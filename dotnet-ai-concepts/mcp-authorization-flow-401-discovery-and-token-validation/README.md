# MCP Authorization Flow: 401, Discovery and Token Validation

## Goal

Protect an MCP server with OAuth and read the whole flow from its wire log: the 401 with a pointer to the
protected resource metadata, discovery of the authorization server, authorization code with PKCE and `resource`,
a token whose audience is this server alone, and the difference between a 401 (the token is not good for this
server) and a 403 (it is, but the scope is not enough). You complete the protected server, a minimal test
authorization server, and the log that records every hop without leaking the tokens.

## Prerequisites

- OAuth 2.0 authorization code flow and PKCE (see the OAuth 2.0 from First Principles lesson)
- ASP.NET Core authentication schemes and authorization policies
- MCP over Streamable HTTP (see the MCP Servers and Clients lesson)
- .NET 10 SDK

## Task

Three projects in `src/` and one test project:

- `McpAuthLab.Server` is the MCP server (port 5900 in the URLs) with one tool, `whoami`.
- `McpAuthLab.AuthServer` is a test-only authorization server (port 5901): one pre-registered client, no login, no
  consent, any scope granted, a throwaway RSA key generated at startup. It is not a model of a real server.
- `McpAuthLab.Wire` has the URLs, the `WireLogHandler` that logs every request and response, and the routing handler.

Nothing listens on a port. Each server runs on an in-memory `TestServer`, and the client's `HttpClient` routes each
URL to the right one by port, so the requests are real HTTP messages without a socket. The real SDK `McpClient`
runs the OAuth flow. Every test that talks to the client prints the wire log to the test output (run
`dotnet test --logger "console;verbosity=detailed"` and read it: that is the point of the lab).

## Instructions

Complete the following TODOs:

- TODO-00: `McpAuthLab.Server/Program.cs`: the default schemes and `AddMcp` with the resource metadata (the 401 and the metadata document).
- TODO-01: `Program.cs`: JWT bearer with issuer and audience validation, and the events that record why a token was accepted or rejected.
- TODO-02: `Program.cs`: the `mcp` policy that checks the `scope` claim, required on the MCP endpoint.
- TODO-03: `Program.cs`: the middleware that adds `scope` to the 401 and builds the `insufficient_scope` 403.
- TODO-04: `McpAuthLab.AuthServer/Program.cs`: PKCE S256 at the token endpoint.
- TODO-05: `McpAuthLab.AuthServer/Program.cs`: the `resource` parameter, required at `/authorize` and checked at `/token`.
- TODO-06: `McpAuthLab.AuthServer/Program.cs`: the `iss` parameter in the authorization response (RFC 9207).
- TODO-07: `McpAuthLab.Wire/WireLog.cs`: `WireRedaction.Shorten`, so that tokens and codes never sit whole in the log.

Run the tests until they all pass. Things to notice while you read the logs:

- the same 401 header answers "no token", "token for another server" and "not a token at all": where does the
  reason go?
- `resource` travels in the authorization request and in the token request, `scope` comes from the challenge, and
  the token only ever travels in `Authorization`;
- three of the four `iss` cases fail on the client before `POST /token` appears in the log.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

14 tests (the `iss` test has four rows); 13 fail until the TODOs are done.

## Bonus (Optional)

- TODO-08 (optional): `KeyRing.Rotate`: replace the signing key. In a scratch test, get a token, rotate, get a new
  token and send both to the protected server. Which one does it accept, and what does the server log say?
