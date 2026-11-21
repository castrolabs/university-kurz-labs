using System.Security.Cryptography;
using System.Text;

namespace LdapLab;

public sealed class NoSuchObjectException(string dn) : Exception($"No such object (32): {dn}");

public enum SearchScope { Base, OneLevel, Subtree }

/// <summary>LDAP result codes the lab cares about.</summary>
public enum BindStatus { Success = 0, InvalidCredentials = 49, UnwillingToPerform = 53 }

/// <summary>Identity is what "whoami" would print: "anonymous" or "dn:" followed by the bound DN.</summary>
public sealed record BindResult(BindStatus Status, string Identity);

public sealed class Entry(DistinguishedName dn, IDictionary<string, string[]> attributes)
{
    private readonly Dictionary<string, string[]> _attributes = new(attributes, StringComparer.OrdinalIgnoreCase);

    public DistinguishedName Dn { get; } = dn;

    /// <summary>Values of an attribute (names are case-insensitive); empty when the attribute is not defined.</summary>
    public string[] Values(string attribute) => _attributes.TryGetValue(attribute, out var v) ? v : [];
}

/// <summary>userPassword as OpenLDAP stores it: {SSHA} plus Base64(SHA-1(password + salt) + salt).</summary>
public static class Ssha
{
    public static string Hash(string password, byte[] salt)
    {
        var digest = SHA1.HashData([.. Encoding.UTF8.GetBytes(password), .. salt]);
        return "{SSHA}" + Convert.ToBase64String([.. digest, .. salt]);
    }

    public static bool Verify(string password, string stored)
    {
        if (!stored.StartsWith("{SSHA}", StringComparison.Ordinal)) return false;
        byte[] raw;
        try { raw = Convert.FromBase64String(stored["{SSHA}".Length..]); } catch (FormatException) { return false; }
        if (raw.Length <= 20) return false;
        var expected = Hash(password, raw[20..]);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(stored));
    }
}

/// <summary>
/// A tiny in-memory directory with LDAP semantics. There is no server and no network: Search takes
/// the filter as the string an LDAP client would send, and Bind answers like a server would.
/// </summary>
/// <param name="allowUnauthenticatedBind">
/// The old OpenLDAP setting "olcAllows: bind_anon_dn": a bind with a DN and an empty password
/// succeeds as anonymous instead of being refused.
/// </param>
public sealed class InMemoryDirectory(bool allowUnauthenticatedBind = false)
{
    private readonly List<Entry> _entries = [];

    public void Add(string dn, IDictionary<string, string[]> attributes) =>
        _entries.Add(new Entry(DistinguishedName.Parse(dn), attributes));

    // TODO-06: Search. Parse the filter string (a bad filter throws FilterException), throw
    // NoSuchObjectException when the base entry does not exist, then return the entries that are in
    // scope (Base: the base itself, OneLevel: its direct children, Subtree: the base and everything
    // below it) and match the filter.
    public IReadOnlyList<Entry> Search(string baseDn, SearchScope scope, string filter)
    {
        var parsed = LdapFilter.Parse(filter);
        var root = DistinguishedName.Parse(baseDn);
        if (root.Rdns.Count > 0 && !_entries.Any(e => e.Dn.IsEquivalentTo(root)))
            throw new NoSuchObjectException(baseDn);
        return _entries
            .Where(e => scope switch
            {
                SearchScope.Base => e.Dn.IsEquivalentTo(root),
                SearchScope.OneLevel => e.Dn.Rdns.Count == root.Rdns.Count + 1 && e.Dn.Parent.IsEquivalentTo(root),
                _ => e.Dn.IsDescendantOrSelfOf(root),
            })
            .Where(e => e.Matches(parsed))
            .ToList();
    }

    // TODO-07: Simple bind (RFC 4513). No DN and no password is an anonymous bind that succeeds.
    // A DN with an empty password is an "unauthenticated bind": refuse it with UnwillingToPerform,
    // unless allowUnauthenticatedBind is set, in which case it SUCCEEDS but as "anonymous" (that is
    // the trap). Otherwise look the entry up by DN (case and spacing do not matter) and check one
    // of its userPassword values with Ssha.Verify. An unknown DN and a wrong password must be
    // indistinguishable: both InvalidCredentials, so the server does not leak which users exist.
    public BindResult Bind(string dn, string password)
    {
        if (dn.Length == 0) return password.Length == 0
            ? new BindResult(BindStatus.Success, "anonymous")
            : new BindResult(BindStatus.InvalidCredentials, "anonymous");
        if (password.Length == 0) return allowUnauthenticatedBind
            ? new BindResult(BindStatus.Success, "anonymous")
            : new BindResult(BindStatus.UnwillingToPerform, "anonymous");
        DistinguishedName parsed;
        try { parsed = DistinguishedName.Parse(dn); }
        catch (FormatException) { return new BindResult(BindStatus.InvalidCredentials, "anonymous"); }
        var entry = _entries.FirstOrDefault(e => e.Dn.IsEquivalentTo(parsed));
        return entry is not null && entry.Values("userPassword").Any(h => Ssha.Verify(password, h))
            ? new BindResult(BindStatus.Success, $"dn:{entry.Dn}")
            : new BindResult(BindStatus.InvalidCredentials, "anonymous");
    }

    // TODO-09 (optional): Compare (RFC 4511): true when the entry has that exact value for the attribute
    // (case-insensitive, no hashing). Notice what it says for ("userPassword", "correct horse").
    public bool Compare(string dn, string attribute, string value)
    {
        var parsed = DistinguishedName.Parse(dn);
        var entry = _entries.FirstOrDefault(e => e.Dn.IsEquivalentTo(parsed)) ?? throw new NoSuchObjectException(dn);
        return entry.Values(attribute).Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
    }
}
