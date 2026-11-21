# MCP Authorization Flow: 401, Discovery and Token Validation (solution)

## Goal

The official solution: a protected MCP server, a minimal test authorization server and a redacting wire log, with 14
tests that read the flow from the outside.

## Prerequisites

- .NET 10 SDK

## Task

Same as the starter. Every TODO is implemented, and the optional one too.

## Instructions

```bash
dotnet test
dotnet test --logger "console;verbosity=detailed"   # prints the wire log of each flow test
```

## Solution notes

- **Two schemes, two jobs.** `DefaultAuthenticateScheme` is JWT bearer (it reads the token) and
  `DefaultChallengeScheme` is the MCP scheme (it writes `401` with `resource_metadata`). `AddMcp` also maps
  `/.well-known/oauth-protected-resource/mcp`, whose `resource` is derived from the request path: the root document
  names another resource.
- **The audience is the endpoint URI.** `ValidAudience = http://localhost:5900/mcp` must equal what the client sends
  as `resource`. A token for `http://localhost:5910/mcp` fails with `IDX10214` and a garbage token with `IDX14102`,
  both as the same 401: no `invalid_token`, one challenge for every reason. The only place the reason shows up is
  `OnAuthenticationFailed`, which is why the lab records it in `AuthDecisions`.
- **401 against 403.** A token that is valid but has `scope=openid` passes authentication and fails the `mcp`
  policy, so the answer is 403. The SDK handler gives a bare 403 and a challenge without `scope`; the middleware
  fills in `scope` on the 401 and `error="insufficient_scope"` on the 403, which is what lets a client step up.
- **PKCE and `resource` live at the authorization server.** It compares `Base64Url(SHA-256(code_verifier))` with
  the challenge stored with the code, and refuses a request without `resource` with `error=invalid_target` in the
  redirect (an error before a code exists goes back to the client). `aud` is the `resource`.
- **RFC 9207.** The client compares the `iss` of the authorization response with the issuer of the validated
  metadata, by exact string: a trailing slash fails. In every failing case `POST /token` is absent from the wire
  log, because the code never left the client. The `authorization_response_iss_parameter_supported` key of the
  metadata is itself 40 characters long, so the log shortens it too (`authoriz...`): the redaction is by length, not
  by meaning.
- **`TimeProvider`.** The test authorization server reads the clock through `TimeProvider` (the system one here);
  the tests do not depend on expiry, so they never wait.
- **TODO-08 (optional).** Measured in a scratch test: after `Rotate()` the token signed with the new key gets a 401
  (`rejected: IDX10503: Signature validation failed ... did not match any keys`), while the token signed with the old
  key is still accepted, because the running server keeps the old keys in its cached configuration until it
  refreshes. A real authorization server publishes the new key in the JWKS before it signs with it. No test covers
  this: it depends on the refresh behavior of the JWT bearer handler.

## Not covered

Client registration (Client ID Metadata Documents, DCR), the authorization extensions, token passthrough, confused
deputy and SSRF in metadata fetches: each has its own lesson.
