using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Time.Testing;

namespace MfaLab.Tests;

public sealed class MfaTests
{
    // The seeds of RFC 4226 / RFC 6238: the same ASCII digits repeated up to the HMAC key size.
    private static readonly byte[] Sha1Seed = Encoding.ASCII.GetBytes("12345678901234567890");
    private static readonly byte[] Sha256Seed = Encoding.ASCII.GetBytes("12345678901234567890123456789012");
    private static readonly byte[] Sha512Seed = Encoding.ASCII.GetBytes("1234567890123456789012345678901234567890123456789012345678901234");

    // A step boundary (1111111110 = 37037037 * 30), so "+15 s" is the middle of a step.
    private const long StepStart = 1_111_111_110;
    private const long ServerTime = StepStart + 15;

    private static string CodeAt(long unixSeconds) => Totp.Compute(Sha1Seed, unixSeconds);

    private static (TotpVerifier Verifier, FakeTimeProvider Clock) NewVerifier(int window = 1, int maxFailures = 5)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(ServerTime));
        return (new TotpVerifier(Sha1Seed, clock, window, maxFailures), clock);
    }

    // --- the algorithms, against the official test vectors ---

    [Fact]
    public void ShouldMatchRfc4226AppendixDAndTruncateToTheRequestedDigits()
    {
        string[] expected = ["755224", "287082", "359152", "969429", "338314", "254676", "287922", "162583", "399871", "520489"];

        for (ulong counter = 0; counter < 10; counter++)
            Assert.Equal(expected[counter], Hotp.Compute(Sha1Seed, counter));

        // The same 31-bit number: 8 digits is modulo 10^8, 6 digits modulo 10^6 (RFC 6238, T=59, counter 1).
        Assert.Equal("94287082", Hotp.Compute(Sha1Seed, 1, digits: 8));
        Assert.Equal("287082", Hotp.Compute(Sha1Seed, 1, digits: 6));
    }

    [Fact]
    public void ShouldMatchRfc6238AppendixBForSha1() =>
        AssertRfc6238(OtpAlgorithm.Sha1, Sha1Seed, "94287082", "07081804", "14050471", "89005924", "69279037", "65353130");

    [Fact]
    public void ShouldMatchRfc6238AppendixBForSha256() =>
        AssertRfc6238(OtpAlgorithm.Sha256, Sha256Seed, "46119246", "68084774", "67062674", "91819424", "90698825", "77737706");

    [Fact]
    public void ShouldMatchRfc6238AppendixBForSha512() =>
        AssertRfc6238(OtpAlgorithm.Sha512, Sha512Seed, "90693936", "25091201", "99943326", "93441116", "38618901", "47863826");

    private static void AssertRfc6238(OtpAlgorithm algorithm, byte[] seed, params string[] expected)
    {
        long[] times = [59, 1_111_111_109, 1_111_111_111, 1_234_567_890, 2_000_000_000, 20_000_000_000];
        for (var i = 0; i < times.Length; i++)
            Assert.True(expected[i] == Totp.Compute(seed, times[i], digits: 8, algorithm: algorithm),
                $"{algorithm} at T={times[i]}: expected {expected[i]}");
    }

    [Fact]
    public void ShouldComputeTheTimeStepWithoutOverflow()
    {
        Assert.Equal(0UL, Totp.Step(29));
        Assert.Equal(1UL, Totp.Step(30));
        Assert.Equal(1UL, Totp.Step(59));
        Assert.Equal(1UL, Totp.Step(60, period: 60));
        Assert.Equal(666_666_666UL, Totp.Step(20_000_000_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => Totp.Step(59, period: 0));
    }

    [Fact]
    public void ShouldCompareCodesWithoutLeakingAndTreatNullAsDifferent()
    {
        Assert.True(CodeComparer.Equal("123456", "123456"));
        Assert.False(CodeComparer.Equal("123456", "123457"));
        Assert.False(CodeComparer.Equal("123456", "12345"));
        Assert.False(CodeComparer.Equal("123456", null));
        Assert.False(CodeComparer.Equal(null, "123456"));
    }

    // --- what the verifier must get right ---

    [Fact]
    public void ShouldAcceptOnlyPhonesInsideTheClockWindow()
    {
        // Phone offset in seconds against a server 15 s into a step: accepted with window 0 / +-1 / +-2.
        (int Offset, bool W0, bool W1, bool W2)[] table =
        [
            (0, true, true, true), (10, true, true, true), (20, false, true, true), (40, false, true, true),
            (-20, false, true, true), (-50, false, false, true), (80, false, false, false),
        ];

        foreach (var row in table)
        {
            var phoneCode = CodeAt(ServerTime + row.Offset);
            foreach (var (window, accepted) in new[] { (0, row.W0), (1, row.W1), (2, row.W2) })
            {
                var (verifier, _) = NewVerifier(window);
                var expected = accepted ? VerifyResult.Ok : VerifyResult.Invalid;
                Assert.True(expected == verifier.Verify(phoneCode), $"phone {row.Offset:+#;-#;0} s, window {window}: expected {expected}");
            }
        }
    }

    [Fact]
    public void ShouldRefuseTheSameCodeAndEveryOlderStepOnceAStepWasUsed()
    {
        var (verifier, clock) = NewVerifier();
        var code = CodeAt(ServerTime);

        Assert.Equal(VerifyResult.Ok, verifier.Verify(code));
        Assert.Equal(VerifyResult.Replay, verifier.Verify(code));
        Assert.Equal(VerifyResult.Replay, verifier.Verify(CodeAt(ServerTime - 30)));

        // The code is still inside the window 30 s later: only the remembered step keeps it from working again.
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(VerifyResult.Replay, verifier.Verify(code));
        Assert.Equal(VerifyResult.Ok, verifier.Verify(CodeAt(ServerTime + 30)));
    }

    [Fact]
    public void ShouldRefuseTheCurrentCodeAfterAcceptingOneFromAPhoneThatRunsAhead()
    {
        var (verifier, _) = NewVerifier(window: 1);

        Assert.Equal(VerifyResult.Ok, verifier.Verify(CodeAt(ServerTime + 30)));   // step n + 1
        Assert.Equal(VerifyResult.Replay, verifier.Verify(CodeAt(ServerTime)));    // step n is now "older"
    }

    [Fact]
    public void ShouldLockTheAccountAfterTooManyFailuresAndResetTheCounterOnSuccess()
    {
        var (verifier, clock) = NewVerifier(maxFailures: 5);
        string[] wrong = ["000000", "111111", "222222", "333333", "444444"];
        Assert.DoesNotContain(CodeAt(ServerTime), wrong);

        // Four failures and a success: the counter goes back to zero.
        foreach (var guess in wrong[..4]) Assert.Equal(VerifyResult.Invalid, verifier.Verify(guess));
        Assert.Equal(VerifyResult.Ok, verifier.Verify(CodeAt(ServerTime)));
        Assert.Equal(0, verifier.Failures);

        // Five failures lock it, and then even the right code is refused.
        clock.Advance(TimeSpan.FromSeconds(30));
        foreach (var guess in wrong) Assert.Equal(VerifyResult.Invalid, verifier.Verify(guess));
        Assert.Equal(VerifyResult.Locked, verifier.Verify(CodeAt(ServerTime + 30)));
    }

    [Fact]
    public void ShouldTreatMalformedCodesAsFailuresInsteadOfThrowing()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(ServerTime));
        var verifier = new TotpVerifier(Sha1Seed, clock, window: 1, maxFailures: 10);

        foreach (var bad in new string?[] { null, "", "12345", "1234567", "abcdef", "12 456" })
            Assert.Equal(VerifyResult.Invalid, verifier.Verify(bad));

        Assert.Equal(6, verifier.Failures);
    }

    // --- recovery codes ---

    [Fact]
    public void ShouldGenerateRandomCodesAndKeepOnlyTheirHashes()
    {
        var recovery = new RecoveryCodes();
        var codes = recovery.Generate();

        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches(new Regex("^[A-Z2-7]{4}(-[A-Z2-7]{4}){3}$"), c));

        Assert.Equal(10, recovery.StoredHashes.Count);
        foreach (var stored in recovery.StoredHashes)
        {
            Assert.Equal(64, stored.Length);   // hex SHA-256
            Assert.DoesNotContain(codes, c => stored.Contains(c.Replace("-", ""), StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ShouldBurnARecoveryCodeOnUseAndInvalidateTheOldSetOnRegeneration()
    {
        var recovery = new RecoveryCodes();
        var codes = recovery.Generate();

        Assert.True(recovery.Redeem(codes[0]));
        Assert.False(recovery.Redeem(codes[0]));                                       // single use
        Assert.True(recovery.Redeem(codes[1].ToLowerInvariant().Replace("-", " ")));   // the user types it loosely
        Assert.False(recovery.Redeem("AAAA-AAAA-AAAA-AAAA"));
        Assert.False(recovery.Redeem(null));

        var fresh = recovery.Generate();
        Assert.False(recovery.Redeem(codes[2]));
        Assert.True(recovery.Redeem(fresh[0]));
    }

    // --- optional ---

    [Fact, Trait("Optional", "true")]
    public void ShouldBuildTheOtpauthUriBehindTheQrCode()
    {
        var uri = OtpAuthUri.Build("Example Corp", "alice@example.org", Sha1Seed);

        Assert.StartsWith("otpauth://totp/", uri);
        var parsed = new Uri(uri);
        Assert.Equal("/Example Corp:alice@example.org", Uri.UnescapeDataString(parsed.AbsolutePath));

        var query = HttpUtility.ParseQueryString(parsed.Query);
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", query["secret"]);   // base32 of the 20-byte test seed
        Assert.Equal("Example Corp", query["issuer"]);
        Assert.Equal("SHA1", query["algorithm"]);
        Assert.Equal("6", query["digits"]);
        Assert.Equal("30", query["period"]);
        Assert.Equal(Sha1Seed, Base32.Decode(query["secret"]!));
    }

    [Fact, Trait("Optional", "true")]
    public void ShouldAcceptAHotpCodeInsideTheLookAheadWindowAndResynchronize()
    {
        var verifier = new HotpVerifier(Sha1Seed, lookAhead: 5);

        Assert.True(verifier.Verify(Hotp.Compute(Sha1Seed, 3)));    // three button presses the server never saw
        Assert.Equal(4UL, verifier.Counter);
        Assert.False(verifier.Verify(Hotp.Compute(Sha1Seed, 3)));   // never twice
        Assert.False(verifier.Verify(Hotp.Compute(Sha1Seed, 10)));  // beyond the look-ahead
        Assert.True(verifier.Verify(Hotp.Compute(Sha1Seed, 9)));    // 4 + 5, the last one inside
    }
}
