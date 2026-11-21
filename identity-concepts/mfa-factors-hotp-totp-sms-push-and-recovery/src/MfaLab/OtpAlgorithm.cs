namespace MfaLab;

/// <summary>The HMAC hash. RFC 6238 defines test vectors for all three.</summary>
public enum OtpAlgorithm
{
    Sha1,
    Sha256,
    Sha512,
}
