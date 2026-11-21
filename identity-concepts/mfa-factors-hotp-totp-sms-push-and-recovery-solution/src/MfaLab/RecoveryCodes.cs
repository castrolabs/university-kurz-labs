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
        _used.Clear();
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var raw = Base32.Encode(RandomNumberGenerator.GetBytes(10));
            codes.Add($"{raw[..4]}-{raw[4..8]}-{raw[8..12]}-{raw[12..]}");
            _used[Hash(raw)] = false;
        }
        return codes;
    }

    public bool Redeem(string? code)
    {
        if (code is null) return false;
        var hash = Hash(Normalize(code));
        if (!_used.TryGetValue(hash, out var used) || used) return false;
        _used[hash] = true;
        return true;
    }

    private static string Normalize(string code) => code.Replace("-", "").Replace(" ", "").ToUpperInvariant();

    private static string Hash(string normalized) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(normalized)));
}
