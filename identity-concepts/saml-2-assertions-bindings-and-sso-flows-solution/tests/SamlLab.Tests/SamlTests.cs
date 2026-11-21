using System.IO.Compression;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Time.Testing;
using SamlLab;

namespace SamlLab.Tests;

public class SamlTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 5, 44, 0, TimeSpan.Zero);

    private readonly TestIdp _idp = new();
    private FakeTimeProvider _time = new(T0);

    public void Dispose() => _idp.Dispose();

    private SamlServiceProvider NewSp(bool allowIdpInitiated = false, X509Pin pin = X509Pin.Idp)
    {
        using var other = pin == X509Pin.Other ? new TestIdp("CN=someone-else") : null;
        var cert = other?.PublicCertificate ?? _idp.PublicCertificate;
        return new SamlServiceProvider(new SpOptions(TestIdp.SpEntityId, TestIdp.AcsUrl, TestIdp.IdpEntityId, cert) { AllowIdpInitiated = allowIdpInitiated }, _time);
    }

    public enum X509Pin { Idp, Other }

    private static string Post(XmlDocument doc) => SamlBindings.EncodePost(doc.OuterXml);

    /// <summary>An SP-initiated login: the SP issues the AuthnRequest, the IdP answers it.</summary>
    private (SamlServiceProvider Sp, XmlDocument Response) SpInitiated(Func<ResponseSpec, ResponseSpec>? change = null)
    {
        var sp = NewSp();
        var (id, _) = sp.CreateAuthnRequest(TestIdp.SsoUrl);
        var spec = new ResponseSpec { InResponseTo = id };
        return (sp, _idp.BuildResponse(_time, change?.Invoke(spec) ?? spec));
    }

    private static SamlValidationException Rejected(Action action) => Assert.Throws<SamlValidationException>(action);

    // ---- bindings

    [Fact]
    public void Redirect_binding_is_raw_deflate_and_post_binding_is_plain_base64()
    {
        var sp = NewSp();
        var (id, url) = sp.BuildRedirect(TestIdp.SsoUrl, "/reports");

        var query = url[(url.IndexOf('?') + 1)..].Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        Assert.Equal("/reports", query["RelayState"]);
        var xml = SamlBindings.DecodeRedirect(query["SAMLRequest"]);
        Assert.Contains($"ID=\"{id}\"", xml);
        Assert.Contains($"Destination=\"{TestIdp.SsoUrl}\"", xml);
        Assert.Contains($"AssertionConsumerServiceURL=\"{TestIdp.AcsUrl}\"", xml);
        Assert.Equal(xml, SamlBindings.DecodeRedirect(SamlBindings.EncodeRedirect(xml)));

        // raw DEFLATE: smaller than plain base64, and neither a zlib nor a gzip stream
        var redirect = SamlBindings.EncodeRedirect(xml);
        Assert.True(redirect.Length < SamlBindings.EncodePost(xml).Length);
        Assert.ThrowsAny<InvalidDataException>(() => new ZLibStream(new MemoryStream(Convert.FromBase64String(redirect)), CompressionMode.Decompress).CopyTo(Stream.Null));

        // POST binding: base64 of the UTF-8 XML, nothing else
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)), SamlBindings.EncodePost(xml));
        Assert.Equal(xml, SamlBindings.DecodePost(SamlBindings.EncodePost(xml)));
    }

    // ---- the happy path and the signature

    [Fact]
    public void A_valid_sp_initiated_response_is_accepted_and_read()
    {
        var (sp, response) = SpInitiated();
        var principal = sp.ProcessPostResponse(Post(response));
        Assert.Equal("alice@example.org", principal.NameId);
        Assert.Equal("session-1", principal.SessionIndex);
        Assert.Equal(["finance-approver"], principal.Attributes["Role"]);
    }

    [Fact]
    public void An_edited_assertion_or_another_pinned_certificate_fails_the_signature_check()
    {
        var (sp, response) = SpInitiated();
        var edited = SamlBindings.EncodePost(response.OuterXml.Replace("alice@example.org", "admin@example.org"));
        Assert.Contains("signature", Rejected(() => sp.ProcessPostResponse(edited)).Message, StringComparison.OrdinalIgnoreCase);

        var strangerSp = NewSp(pin: X509Pin.Other);
        var (id, _) = strangerSp.CreateAuthnRequest(TestIdp.SsoUrl);
        var genuine = _idp.BuildResponse(_time, new ResponseSpec { InResponseTo = id });
        Assert.Contains("signature", Rejected(() => strangerSp.ProcessPostResponse(Post(genuine))).Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_signature_on_the_response_alone_does_not_protect_the_assertion()
    {
        var (sp, response) = SpInitiated(s => s with { SignAssertion = false, SignResponse = true });
        Assert.Contains("not signed", Rejected(() => sp.ProcessPostResponse(Post(response))).Message);
    }

    // ---- XML attacks

    [Fact]
    public void Signature_wrapping_fools_the_naive_verifier_and_not_the_real_one()
    {
        var (sp, genuine) = SpInitiated();
        var cert = _idp.PublicCertificate;
        Assert.Equal("alice@example.org", NaiveVerifier.ReadNameId(genuine, cert));   // control: nothing wrong with the file

        foreach (var attack in new[] { Wrapping.CopyBefore(genuine), Wrapping.HideInAdvice(genuine) })
        {
            // the signature is still valid, the verifier is happy, and it reads the attacker's element
            Assert.Equal("admin@example.org", NaiveVerifier.ReadNameId(attack, cert));
            // the real one refuses a document whose signed assertion is not the only one in its place
            Rejected(() => sp.ProcessPostResponse(Post(attack)));
        }
        Assert.Equal("alice@example.org", sp.ProcessPostResponse(Post(genuine)).NameId);   // and still accepts the genuine one
    }

    [Fact]
    public void A_copy_with_the_same_id_is_refused_and_never_read()
    {
        var (sp, genuine) = SpInitiated();
        var attack = Wrapping.CopyBefore(genuine, keepId: true);
        Rejected(() => sp.ProcessPostResponse(Post(attack)));
    }

    [Fact]
    public void A_doctype_is_refused_before_anything_else_happens()
    {
        var (sp, response) = SpInitiated();
        var withDtd = "<!DOCTYPE r [<!ENTITY x \"expanded\">]>" + response.OuterXml;
        Assert.Contains("XML", Rejected(() => sp.ProcessPostResponse(SamlBindings.EncodePost(withDtd))).Message);
        Rejected(() => sp.ProcessPostResponse("this is not base64!"));
    }

    // ---- what the assertion says

    [Fact]
    public void Issuer_audience_and_status_must_all_be_right()
    {
        foreach (var change in new Func<ResponseSpec, ResponseSpec>[]
        {
            s => s with { Issuer = "http://localhost:6360/realms/other" },
            s => s with { Audience = "http://localhost:6361/other-sp" },
            s => s with { StatusCode = "urn:oasis:names:tc:SAML:2.0:status:Responder" },
        })
        {
            var (sp, response) = SpInitiated(change);
            Rejected(() => sp.ProcessPostResponse(Post(response)));
        }
    }

    [Fact]
    public void The_validity_window_follows_the_injected_clock_and_the_skew()
    {
        // the assertion is valid from T0-1s to T0+60s, and the SP tolerates 30 s of skew
        bool Accepted(TimeSpan advance, TimeSpan? notBefore = null)
        {
            _time = new FakeTimeProvider(T0);   // a fake clock never goes back, so each attempt gets its own
            var (sp, response) = SpInitiated(s => s with { NotBeforeOffset = notBefore ?? TimeSpan.FromSeconds(-1) });
            _time.Advance(advance);
            try { sp.ProcessPostResponse(Post(response)); return true; }
            catch (SamlValidationException) { return false; }
        }

        Assert.True(Accepted(TimeSpan.Zero));
        Assert.True(Accepted(TimeSpan.FromSeconds(89)));           // expired for 29 s, inside the skew
        Assert.False(Accepted(TimeSpan.FromSeconds(90)));          // NotOnOrAfter + skew: on the limit is already out
        Assert.False(Accepted(TimeSpan.FromMinutes(10)));
        Assert.True(Accepted(TimeSpan.Zero, notBefore: TimeSpan.FromSeconds(20)));    // not valid yet, but inside the skew
        Assert.False(Accepted(TimeSpan.Zero, notBefore: TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void Destination_and_recipient_must_be_this_sps_acs_exactly()
    {
        foreach (var change in new Func<ResponseSpec, ResponseSpec>[]
        {
            s => s with { Destination = "http://localhost:6362/acs" },
            s => s with { Recipient = "http://localhost:6362/acs" },
            s => s with { Recipient = TestIdp.AcsUrl + "/" },
        })
        {
            var (sp, response) = SpInitiated(change);
            Rejected(() => sp.ProcessPostResponse(Post(response)));
        }
    }

    // ---- request binding and replay

    [Fact]
    public void In_response_to_must_be_a_request_of_ours_and_is_used_once()
    {
        var (sp, response) = SpInitiated();
        Assert.Equal(1, sp.Pending.Count);
        var post = Post(response);
        sp.ProcessPostResponse(post);
        Assert.Equal(0, sp.Pending.Count);
        Assert.Contains("InResponseTo", Rejected(() => sp.ProcessPostResponse(post)).Message);   // the same response, posted twice

        var (sp2, forged) = SpInitiated(s => s with { InResponseTo = "_never-issued" });
        Assert.Contains("InResponseTo", Rejected(() => sp2.ProcessPostResponse(Post(forged))).Message);

        var unsolicited = _idp.BuildResponse(_time, new ResponseSpec());   // IdP-initiated: no InResponseTo at all
        Assert.Contains("InResponseTo", Rejected(() => NewSp().ProcessPostResponse(Post(unsolicited))).Message);
    }

    [Fact]
    public void An_idp_initiated_response_is_protected_by_the_replay_cache_when_it_is_allowed()
    {
        var sp = NewSp(allowIdpInitiated: true);
        var first = Post(_idp.BuildResponse(_time, new ResponseSpec { AssertionId = "_once" }));
        Assert.Equal("alice@example.org", sp.ProcessPostResponse(first).NameId);
        Assert.Contains("already been received", Rejected(() => sp.ProcessPostResponse(first)).Message);

        var another = Post(_idp.BuildResponse(_time, new ResponseSpec { AssertionId = "_twice" }));
        Assert.Equal("alice@example.org", sp.ProcessPostResponse(another).NameId);
        Assert.Equal(2, sp.Replays.Count);
    }

    [Fact]
    public void The_replay_cache_forgets_an_id_once_its_assertion_can_no_longer_be_accepted()
    {
        var cache = new ReplayCache(_time);
        Assert.True(cache.TryRegister("a", _time.GetUtcNow().AddMinutes(1)));
        Assert.False(cache.TryRegister("a", _time.GetUtcNow().AddMinutes(1)));
        Assert.True(cache.TryRegister("b", _time.GetUtcNow().AddMinutes(10)));
        Assert.Equal(2, cache.Count);

        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.True(cache.TryRegister("c", _time.GetUtcNow().AddMinutes(1)));   // "a" expired and was purged on the way
        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryRegister("b", _time.GetUtcNow().AddMinutes(10)));
    }
}
