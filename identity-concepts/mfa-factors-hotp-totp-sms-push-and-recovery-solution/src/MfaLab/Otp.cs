using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace MfaLab;

/// <summary>HOTP, RFC 4226: HMAC of an 8-byte big-endian counter, dynamic truncation, last N decimal digits.</summary>
public static class Hotp
{
    public static string Compute(ReadOnlySpan<byte> key, ulong counter, int digits = 6, OtpAlgorithm algorithm = OtpAlgorithm.Sha1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digits, 6);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digits, 8);

        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(message, counter);

        var hmac = algorithm switch
        {
            OtpAlgorithm.Sha1 => HMACSHA1.HashData(key, message),
            OtpAlgorithm.Sha256 => HMACSHA256.HashData(key, message),
            OtpAlgorithm.Sha512 => HMACSHA512.HashData(key, message),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        var offset = hmac[^1] & 0x0F;
        var binary = BinaryPrimitives.ReadUInt32BigEndian(hmac.AsSpan(offset, 4)) & 0x7FFFFFFF;
        var modulus = (uint)Math.Pow(10, digits);
        return (binary % modulus).ToString($"D{digits}");
    }
}

/// <summary>TOTP, RFC 6238: HOTP with the counter floor(unix_time / period).</summary>
public static class Totp
{
    public const int DefaultPeriod = 30;

    public static ulong Step(long unixSeconds, int period = DefaultPeriod)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unixSeconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        return (ulong)(unixSeconds / period);
    }

    public static string Compute(ReadOnlySpan<byte> key, long unixSeconds, int digits = 6,
        OtpAlgorithm algorithm = OtpAlgorithm.Sha1, int period = DefaultPeriod) =>
        Hotp.Compute(key, Step(unixSeconds, period), digits, algorithm);
}

public static class CodeComparer
{
    public static bool Equal(string? expected, string? actual)
    {
        if (expected is null || actual is null) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }
}
