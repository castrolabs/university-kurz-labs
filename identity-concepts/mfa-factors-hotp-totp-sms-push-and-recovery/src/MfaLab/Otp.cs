using System.Security.Cryptography;

namespace MfaLab;

/// <summary>HOTP, RFC 4226: HMAC of an 8-byte big-endian counter, dynamic truncation, last N decimal digits.</summary>
public static class Hotp
{
    public static string Compute(ReadOnlySpan<byte> key, ulong counter, int digits = 6, OtpAlgorithm algorithm = OtpAlgorithm.Sha1)
    {
        // TODO-00: Implement RFC 4226 from scratch (no OTP library):
        //          1. HMAC (HMACSHA1/HMACSHA256/HMACSHA512 per `algorithm`) of the counter as 8 big-endian bytes.
        //          2. Dynamic truncation: the low nibble of the last byte is the offset; take 4 bytes there and clear the sign bit.
        //          3. Keep the last `digits` decimal digits, left-padded with zeros. Accept 6 to 8 digits.
        //          Check yourself against RFC 4226 Appendix D (secret "12345678901234567890", counter 0 gives 755224).
        throw new NotImplementedException();
    }
}

/// <summary>TOTP, RFC 6238: HOTP with the counter floor(unix_time / period).</summary>
public static class Totp
{
    public const int DefaultPeriod = 30;

    public static ulong Step(long unixSeconds, int period = DefaultPeriod)
    {
        // TODO-01: The time step is floor(unixSeconds / period). Reject a period below 1 and negative times with
        //          ArgumentOutOfRangeException. The result must survive the year 2603 (20_000_000_000 s).
        throw new NotImplementedException();
    }

    public static string Compute(ReadOnlySpan<byte> key, long unixSeconds, int digits = 6,
        OtpAlgorithm algorithm = OtpAlgorithm.Sha1, int period = DefaultPeriod)
    {
        // TODO-02: TOTP is HOTP over the time step.
        throw new NotImplementedException();
    }
}

public static class CodeComparer
{
    public static bool Equal(string? expected, string? actual)
    {
        // TODO-03: Compare in constant time: no early exit on the first differing character (a verifier that
        //          returns early leaks how many leading digits were right). null on either side is false.
        throw new NotImplementedException();
    }
}
