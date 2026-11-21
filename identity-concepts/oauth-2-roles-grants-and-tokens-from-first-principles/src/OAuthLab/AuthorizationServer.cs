namespace OAuthLab;

public sealed partial class AuthorizationServer
{
    /// <summary>GET /authorize for a user who is already signed in as <paramref name="userId"/> and has consented.</summary>
    public AuthorizeResponse Authorize(AuthorizeRequest request, string userId)
    {
        // TODO-01: Validate the client and the redirect URI FIRST. An unknown client or a redirect_uri that is not
        //          EXACTLY one of the registered strings (ordinal comparison: no prefix, no wildcard, no case folding,
        //          no extra query) gets an ErrorPage and the browser is never redirected: the URI is not trustworthy.
        //
        // TODO-02: Only now can errors go back to the client with Fail(request, error):
        //          - response_type must be "code" (anything else, the implicit "token" included): unsupported_response_type.
        //          - the client must be allowed the authorization_code grant: unauthorized_client.
        //          - PKCE is mandatory: a missing code_challenge or a method other than "S256" (so no "plain"): invalid_request.
        //          - every requested scope must be registered for the client AND exist in the server: invalid_scope.
        //          Then store an AuthCode (CodeLifetime, bound to client, redirect URI, challenge, user and scope; unused) with a
        //          random value (NewToken()) and return a Redirect with code, state (when given) and iss (Issuer).
        throw new NotImplementedException();
    }

    /// <summary>POST /token.</summary>
    public TokenResponse Token(TokenRequest request)
    {
        // TODO-03: Route by grant_type to ExchangeCode, ClientCredentials and Refresh. Everything else is
        //          unsupported_grant_type (HTTP 400): that includes "password" (ROPC) and "implicit", which OAuth 2.1 removed.
        throw new NotImplementedException();
    }

    private TokenResponse ExchangeCode(TokenRequest request)
    {
        // TODO-04: Authenticate the client (AuthenticateClient; a confidential client must present its secret) and check that
        //          it may use the authorization_code grant (unauthorized_client). Then every problem with the code is
        //          invalid_grant: unknown code, expired code, a code issued to another client, a redirect_uri that is not
        //          exactly the one of the authorize request, a missing or wrong code_verifier (Pkce.Verify).
        //          A code is single use: mark it used as soon as it is presented, even when the rest fails
        //          (a wrong verifier burns it). On success mint a family (NewFamily), remember its id in the code
        //          (FamilyId) and IssueTokens with a refresh token only when the client has the refresh_token grant.
        //
        // TODO-05: A code that is presented AGAIN means someone else holds it. Besides invalid_grant, revoke everything
        //          already issued from it (RevokeFamily on the code's FamilyId, when it has one). RFC 6749 section 4.1.2.
        throw new NotImplementedException();
    }

    private TokenResponse ClientCredentials(TokenRequest request)
    {
        // TODO-06: Authenticate the client. Only a confidential client with the client_credentials grant may use it
        //          (a public client: unauthorized_client). The scope must be within the client's scopes (invalid_scope);
        //          an omitted scope means all of them. The subject is "service-account-<client id>". No refresh token: the
        //          client simply asks again.
        throw new NotImplementedException();
    }

    private TokenResponse Refresh(TokenRequest request)
    {
        // TODO-07: Authenticate the client and check the refresh_token grant. The refresh token must exist, belong to this
        //          client, not be expired nor revoked, and its family must be alive, otherwise invalid_grant.
        //          Rotate: mark it used and IssueTokens (a new access and a new refresh token, same family).
        //          A refresh token that was ALREADY used is a replay: the server cannot tell the thief from the user, so
        //          revoke the whole family (the newest refresh token and access token die too) and answer invalid_grant.
        throw new NotImplementedException();
    }

    /// <summary>RFC 7009: the answer is the same for a token that was revoked and one that never existed.</summary>
    public void Revoke(string? token)
    {
        // TODO-09 (optional): Revoking an access token kills only that token. Revoking a refresh token kills its whole family
        //                    (the access tokens minted from the same grant too). An unknown or null token is silently
        //                    accepted: never throw, never reveal whether it existed.
        throw new NotImplementedException();
    }
}
