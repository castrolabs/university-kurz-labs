namespace OAuthLab;

public sealed partial class AuthorizationServer
{
    /// <summary>GET /authorize for a user who is already signed in as <paramref name="userId"/> and has consented.</summary>
    public AuthorizeResponse Authorize(AuthorizeRequest request, string userId)
    {
        if (request.ClientId is null || !_clients.TryGetValue(request.ClientId, out var client))
            return new ErrorPage("invalid_client");
        if (request.RedirectUri is null || !client.RedirectUris.Contains(request.RedirectUri, StringComparer.Ordinal))
            return new ErrorPage("invalid_redirect_uri");

        if (request.ResponseType != "code") return Fail(request, "unsupported_response_type");
        if (!client.Grants.Contains("authorization_code")) return Fail(request, "unauthorized_client");
        if (string.IsNullOrEmpty(request.CodeChallenge) || request.CodeChallengeMethod != "S256") return Fail(request, "invalid_request");

        var scopes = SplitScope(request.Scope);
        if (scopes.Any(s => !client.Scopes.Contains(s) || !ScopeAudience.ContainsKey(s))) return Fail(request, "invalid_scope");

        var code = new AuthCode
        {
            Value = NewToken(),
            ClientId = client.Id,
            RedirectUri = request.RedirectUri,
            CodeChallenge = request.CodeChallenge,
            UserId = userId,
            Scope = string.Join(' ', scopes),
            ExpiresAt = _time.GetUtcNow() + CodeLifetime,
        };
        _codes[code.Value] = code;

        var parameters = new Dictionary<string, string> { ["code"] = code.Value, ["iss"] = Issuer };
        if (request.State is not null) parameters["state"] = request.State;
        return new Redirect(request.RedirectUri, parameters);
    }

    /// <summary>POST /token.</summary>
    public TokenResponse Token(TokenRequest request) => request.GrantType switch
    {
        "authorization_code" => ExchangeCode(request),
        "client_credentials" => ClientCredentials(request),
        "refresh_token" => Refresh(request),
        _ => TokenResponse.Failure("unsupported_grant_type"),
    };

    private TokenResponse ExchangeCode(TokenRequest request)
    {
        var failure = AuthenticateClient(request.ClientId, request.ClientSecret, out var client);
        if (failure is not null) return failure;
        if (!client.Grants.Contains("authorization_code")) return TokenResponse.Failure("unauthorized_client");

        var invalidGrant = TokenResponse.Failure("invalid_grant");
        if (request.Code is null || !_codes.TryGetValue(request.Code, out var code)) return invalidGrant;

        if (code.Used)
        {
            if (code.FamilyId is not null) RevokeFamily(code.FamilyId);
            return invalidGrant;
        }
        code.Used = true;

        if (_time.GetUtcNow() >= code.ExpiresAt) return invalidGrant;
        if (code.ClientId != client.Id) return invalidGrant;
        if (!string.Equals(request.RedirectUri, code.RedirectUri, StringComparison.Ordinal)) return invalidGrant;
        if (request.CodeVerifier is null || !Pkce.Verify(request.CodeVerifier, code.CodeChallenge)) return invalidGrant;

        var family = NewFamily(client, code.UserId, code.Scope);
        code.FamilyId = family.Id;
        return IssueTokens(family, withRefreshToken: client.Grants.Contains("refresh_token"));
    }

    private TokenResponse ClientCredentials(TokenRequest request)
    {
        var failure = AuthenticateClient(request.ClientId, request.ClientSecret, out var client);
        if (failure is not null) return failure;
        if (!client.IsConfidential || !client.Grants.Contains("client_credentials")) return TokenResponse.Failure("unauthorized_client");

        var scopes = SplitScope(request.Scope);
        if (scopes.Length == 0) scopes = [.. client.Scopes];
        if (scopes.Any(s => !client.Scopes.Contains(s))) return TokenResponse.Failure("invalid_scope");

        var family = NewFamily(client, $"service-account-{client.Id}", string.Join(' ', scopes));
        return IssueTokens(family, withRefreshToken: false);
    }

    private TokenResponse Refresh(TokenRequest request)
    {
        var failure = AuthenticateClient(request.ClientId, request.ClientSecret, out var client);
        if (failure is not null) return failure;
        if (!client.Grants.Contains("refresh_token")) return TokenResponse.Failure("unauthorized_client");

        var invalidGrant = TokenResponse.Failure("invalid_grant");
        if (request.RefreshToken is null || !_refreshTokens.TryGetValue(request.RefreshToken, out var refresh)) return invalidGrant;

        var family = _families[refresh.FamilyId];
        if (family.ClientId != client.Id) return invalidGrant;
        if (refresh.Used)
        {
            RevokeFamily(family.Id);
            return invalidGrant;
        }
        if (refresh.Revoked || family.Revoked || _time.GetUtcNow() >= refresh.ExpiresAt) return invalidGrant;

        refresh.Used = true;
        return IssueTokens(family, withRefreshToken: true);
    }

    /// <summary>RFC 7009: the answer is the same for a token that was revoked and one that never existed.</summary>
    public void Revoke(string? token)
    {
        if (token is null) return;
        if (_accessTokens.TryGetValue(token, out var access)) access.Revoked = true;
        else if (_refreshTokens.TryGetValue(token, out var refresh)) RevokeFamily(refresh.FamilyId);
    }
}
