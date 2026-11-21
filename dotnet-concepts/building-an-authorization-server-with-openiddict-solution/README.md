# Building an Authorization Server with OpenIddict

## Goal

Build an OAuth 2.0 and OpenID Connect authorization server inside ASP.NET Core with OpenIddict 7.9.0, then
verify from the outside what the lesson measured: the flows that are on and off in the discovery document,
client credentials and authorization code with PKCE, scopes that become audiences, claims that only travel
when they have a destination, the token format, replay detection, revocation, and key rotation.

## Prerequisites

- The OAuth 2.0 and OpenID Connect lessons of the identity track, and the OAuth clients lesson (this lab is the server side)
- Minimal APIs and `WebApplicationFactory`
- .NET 10 SDK

## Task

The server is `src/AuthServerLab`. OpenIddict parses and validates the protocol messages, stores applications,
scopes, authorizations and tokens, and signs what you tell it to sign. The login, the user store and the
endpoints that decide what goes into a token are yours. Storage is an in-memory SQLite database created per host
(`DataSource=:memory:` on a connection that stays open): there is no file, no Docker and no port, because the tests
call the server through `WebApplicationFactory` (TestServer). Keys are throwaway self-signed certificates
generated in memory. EF Core's `UseInMemoryDatabase` is deliberately not used: OpenIddict revokes the tokens of a
chain with a bulk update, which that provider does not support (the revocation fails with a logged warning).

Provided: `Program.cs`, the `/login` form (one demo user, `alice`, no password), the protected `/api/whoami`
endpoint (answers with the claims of the validated access token), `LabOptions` (switches the tests flip), the key
provider, and the seeding worker. The tests call `/connect/token`, `/connect/authorize`, `/connect/revoke`, the
discovery document and `/api/whoami`, and some of them read the tokens themselves.

## Instructions

Complete the following TODOs (already done in this solution):

- TODO-00: `AuthServerSetup.ConfigureEndpointsAndFlows`: endpoint URIs, only the three flows that are needed, registered scopes, ASP.NET Core integration with passthrough.
- TODO-01: `AuthServerSetup.ConfigurePkce`: require PKCE and accept only S256.
- TODO-02: `AuthServerSetup.ConfigureKeys`: every certificate of the provider as a signing credential, plus the encryption key.
- TODO-03: `AuthServerSetup.ConfigureTokenFormat`: access token encryption, reference tokens and the refresh reuse leeway, from the `LabOptions` switches.
- TODO-04: `AuthServerSetup.ConfigureValidation`: validate against the local server and the database, so a revoked token is refused.
- TODO-05: `Seed.Applications` and `Seed.ScopeDescriptors`: the `worker` and `spa` clients with their allow-lists of permissions, and the `orders` scope with its resource.
- TODO-06: `ClaimDestinations.Resolve`: which claim goes into which token.
- TODO-07: `Endpoints.Token`: client credentials, and re-signing the principal OpenIddict rebuilt for codes and refresh tokens.
- TODO-08: `Endpoints.Authorize`: send an anonymous browser to the login, build the principal, record the authorization, sign in.

Run the tests until they all pass. Notice what an error looks like on the token endpoint (JSON, status 400 or 401) and on
the authorization endpoint (a 400 with plain `error:` lines, never a redirect to an unverified URI).

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## Bonus (Optional)

- TODO-09 (optional): In `ConfigureTokenFormat`, also apply `lab.AccessTokenLifetime`, then write a test that reads `expires_in` from the token response with a shorter lifetime.
