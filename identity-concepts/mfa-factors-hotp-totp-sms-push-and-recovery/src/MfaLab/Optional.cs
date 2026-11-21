namespace MfaLab;

/// <summary>The text behind the QR code on the enrollment page (Google Authenticator "Key URI Format").</summary>
public static class OtpAuthUri
{
    public static string Build(string issuer, string account, byte[] secret, int digits = 6, int period = 30,
        OtpAlgorithm algorithm = OtpAlgorithm.Sha1)
    {
        // TODO-09 (optional): Build otpauth://totp/{issuer}:{account}?secret=...&issuer=...&algorithm=...&digits=...&period=...
        //                    The secret is base32 without padding (Base32.Encode), not hex and not base64. Percent-encode
        //                    the issuer and the account (Uri.EscapeDataString). The algorithm is written SHA1, SHA256 or SHA512.
        throw new NotImplementedException();
    }
}

/// <summary>
/// HOTP has the mirror problem of the clock window: the counter advances on every button press, so the server
/// accepts a look-ahead window and resynchronizes (RFC 4226, section 7.4).
/// </summary>
public sealed class HotpVerifier(byte[] secret, int lookAhead = 10, int digits = 6)
{
    /// <summary>The next counter the server expects.</summary>
    public ulong Counter { get; private set; }

    public bool Verify(string? code)
    {
        // TODO-10 (optional): Try the counters from `Counter` to `Counter + lookAhead` (inclusive). On a match move
        //                    `Counter` to match + 1 (so the same code never works twice) and return true.
        throw new NotImplementedException();
    }
}
