namespace MfaLab;

/// <summary>The text behind the QR code on the enrollment page (Google Authenticator "Key URI Format").</summary>
public static class OtpAuthUri
{
    public static string Build(string issuer, string account, byte[] secret, int digits = 6, int period = 30,
        OtpAlgorithm algorithm = OtpAlgorithm.Sha1)
    {
        var label = $"{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}";
        var alg = algorithm.ToString().ToUpperInvariant();
        return $"otpauth://totp/{label}?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm={alg}&digits={digits}&period={period}";
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
        for (var candidate = Counter; candidate <= Counter + (ulong)lookAhead; candidate++)
        {
            if (!CodeComparer.Equal(Hotp.Compute(secret, candidate, digits), code)) continue;
            Counter = candidate + 1;
            return true;
        }
        return false;
    }
}
