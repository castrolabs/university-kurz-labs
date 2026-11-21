# Testing MCP Servers In Process and with the Inspector (solution)

## Goal

The official solution: 13 MSTest tests, an `InProcessMcp` helper, a hand-written snapshot helper and a checked-in
`tools/list` snapshot, on MSTest 4.5.3 and Microsoft.Testing.Platform.

## Prerequisites

- .NET 10 SDK

## Task

Same as the starter. Every test is written, the helpers are implemented, and the approved snapshot is checked in.

## Instructions

```bash
dotnet test
```

## Solution notes

- **`InProcessMcp`** builds two `Pipe`s and a generic host with `WithStreamServerTransport(...)` and
  `WithTools<TTools>()`, starts it, and connects `McpClient.CreateAsync` with a `StreamClientTransport` on the other
  ends. The host starts the server as a hosted service, as it does over stdio. Every test builds its own pair, so the
  tests run in parallel (`MSTestSettings.cs`).
- **Three failure contracts.** An ordinary exception gives `IsError = true` and `An error occurred invoking 'get_stock'.`
  (the message stays on the server). An `McpException` keeps its message on purpose. An unknown tool is the JSON-RPC
  error `-32602`: `CallToolAsync` throws `McpProtocolException` with `McpErrorCode.InvalidParams`. A success has
  `IsError == null`, so `Assert.AreNotEqual(true, result.IsError)` is the check.
- **HTTP catches what the pipes cannot.** Routing and the session mode: stateless gives no session id, negotiates
  `2026-07-28`, and answers a plain `GET /mcp` with 405. Authorization, `AllowedHosts` and middleware would be covered
  here too.
- **Fake time.** `McpClientOptions.TimeProvider` drives the initialization timeout and the discover probe. The test
  reads the first request from the peer before it calls `Advance`: once the client has written `initialize`, its timer
  exists. 59 seconds is not enough and 61 is. For an old server that ignores `server/discover`, `Advance(5.1 s)` makes
  the client send `notifications/cancelled` and then `initialize` on the same connection. It does not control
  `HttpClient.Timeout`, OAuth expiry or `ConnectionTimeout`.
- **The snapshot** renders name, description and `inputSchema`, sorted by name. The two transports share one file
  through the `test:` argument of `Verify`. The rug-pull test compares the approved server with `TamperedStockTools`
  and checks that the diff holds the injected sentence and the new `note` parameter: the snapshot does not decide
  whether a change is safe, it forces a human to look at it. `*.received.txt` is git-ignored.
- **Not in the suite.** The Inspector (docs only, see the starter README) and everything about model behavior: whether
  a model picks `get_stock` for "do we have SKU-1?" is evaluation, not contract.

## Bonus notes

Mutation check, run against this solution. Each change was tried on its own:

| Change to the production code | Tests that fail |
| --- | --- |
| The description of `reserve` gets two more words | `ShouldListTheTwoToolsWithTheirDescriptions` and both snapshot tests |
| `McpException` becomes `InvalidOperationException` for quantity 0 | `ShouldShowTheMessageOfAnMcpExceptionOnPurpose` |
| Session mode `Stateful` | `ShouldServeTheToolsOverHttpWithNoSessionInStatelessMode`, `ShouldRefuseAPlainGetOnTheMcpRoute` (the HTTP snapshot stays green: the tool list is the same) |
| `MapMcp` removed | the two HTTP tests and the HTTP snapshot (the pipes cannot see it) |
| `reserve` also refuses quantity 1 | nothing: no test uses quantity 1, so this is the gap to close |

Renaming a parameter was not run; the snapshot renders the schema, so it would fail both snapshot tests.
