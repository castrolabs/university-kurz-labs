using System.Security.Cryptography;
using System.Text;

namespace MfaLab;

/// <summary>Single-use look-up secrets for a user who lost the phone: random, shown once, stored only as a hash.</summary>
public sealed class RecoveryCodes
{
    private readonly Dictionary<string, bool> _used = new();

    /// <summary>What the database holds: hashes only.</summary>
    public IReadOnlyCollection<string> StoredHashes => _used.Keys;

    public IReadOnlyList<string> Generate(int count = 10)
    {
        // TODO-07: Make `count` codes of 80 random bits each (RandomNumberGenerator, never Random) as XXXX-XXXX-XXXX-XXXX in
        //          base32 (Base32.Encode of 10 bytes gives 16 characters). Store ONLY Hash(Normalize(code)) as not used and
        //          return the plain codes (the only time they exist). Generating again replaces the whole set.
        throw new NotImplementedException();
    }

    public bool Redeem(string? code)
    {
        // TODO-08: Normalize the input, hash it and look it up. A known, unused code is burned and returns true;
        //          a used, unknown or null code returns false.
        throw new NotImplementedException();
    }

    private static string Normalize(string code) => code.Replace("-", "").Replace(" ", "").ToUpperInvariant();

    private static string Hash(string normalized) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(normalized)));
}
