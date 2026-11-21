using System.Collections.Concurrent;

namespace McpAuthLab.AuthServer;

public sealed record IssuedCode(
    string ClientId, string RedirectUri, string CodeChallenge, string Resource, string Scope);

/// <summary>Authorization codes waiting to be exchanged. A code is single use.</summary>
public sealed class AuthorizationCodes
{
    private readonly ConcurrentDictionary<string, IssuedCode> _codes = new();

    public string Issue(IssuedCode code)
    {
        var value = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _codes[value] = code;
        return value;
    }

    public IssuedCode? Redeem(string code) => _codes.TryRemove(code, out var issued) ? issued : null;
}
