# OAuth 2: Roles, Grants and Tokens from First Principles

## Goal

Write the core of a small OAuth 2 authorization server and a resource server, so you see why each rule exists:
authorization code with PKCE (S256), exact `redirect_uri` matching, single-use codes whose replay revokes what was
issued, client credentials, refresh token rotation with reuse detection, scope versus audience, and what OAuth 2.1
removed (implicit and the password grant).

## Prerequisites

- The four roles: resource owner, client, authorization server, resource server
- Public versus confidential clients
- .NET 10 SDK

## Task

The concept measured all of this against a real Keycloak in Docker. This lab is OFFLINE: the authorization server is
a plain C# class (`AuthorizationServer`) that you finish, with in-memory state and a `TimeProvider` (the tests use
`FakeTimeProvider`). There is no HTTP, no browser, no Docker and no network, so `Authorize` takes the already
signed-in user and returns what the browser would see, and `Token` takes the form body of `POST /token`. Tokens are
opaque random strings checked by `Introspect` (RFC 7662). Everything is local and the secrets (`web-secret`,
`worker-secret`) are fake.

`ServerInternals.cs` is the plumbing (registered clients `spa`, `web`, `worker`, `legacy`, token minting,
introspection) and `Models.cs` the request and response types: read them, do not edit them. The behavior is in
`Pkce.cs`, `AuthorizationServer.cs` and `ResourceServer.cs`.

## Instructions

Complete the following TODOs:

- TODO-00: `Pkce`: the S256 challenge and a verifier check (43 to 128 unreserved characters, constant-time compare).
- TODO-01: `Authorize`: unknown client or a `redirect_uri` that is not an exact match gets an `ErrorPage`, never a redirect.
- TODO-02: `Authorize`: `response_type=code` only, PKCE with S256 mandatory, scope check, then issue a short-lived code bound to client, redirect URI, challenge, user and scope.
- TODO-03: `Token`: route by `grant_type`; the removed grants (password, implicit) are `unsupported_grant_type`.
- TODO-04: `ExchangeCode`: authenticate the client, every code problem is `invalid_grant`, the code is single use (a wrong verifier burns it).
- TODO-05: `ExchangeCode`: a code presented again also revokes the tokens already issued from it.
- TODO-06: `ClientCredentials`: confidential clients only, scope within the registration, no refresh token.
- TODO-07: `Refresh`: rotate the refresh token; replaying an old one kills the whole family.
- TODO-08: `ResourceServer.Check`: 401 for a dead token, 403 for the wrong audience or a missing scope.

Notice in the tests why a token for `billing-api` is refused by `reports-api` even when the scope string matches, and
what happens to the access token you got from a code that someone else later replays.

Run the tests until they all pass.

## Running the Lab

```bash
dotnet test
```

## Bonus (Optional)

The test of the optional TODO carries `Trait("Optional", "true")`. Skip it with `dotnet test --filter "Optional!=true"`.

- TODO-09 (optional): `Revoke` (RFC 7009): an access token dies alone, a refresh token takes its family with it, garbage is accepted silently.

## Differences from Keycloak 26.8.2 (what the concept measured)

- For a wrong client secret Keycloak answered `unauthorized_client`; RFC 6749 section 5.2 says `invalid_client`, which is what this lab returns (and 401).
- Keycloak answers 400 with an error page for an unregistered `redirect_uri`; here that is the `ErrorPage` result.
- Keycloak still lets a client enable implicit and the password grant; this server has no switch for them, as OAuth 2.1 intends.
- Device authorization (RFC 8628) and JWT access tokens are left out: they are docs only in this lab.
