# MCP Servers and Clients with the C# SDK (solution)

## Goal

The official solution of the lab: the stock MCP server on `ModelContextProtocol` 2.4.0, the client helpers, and 14
tests that read the contract from the outside.

## Prerequisites

- .NET 10 SDK

## Task

Same as the starter. Every TODO is implemented, and the optional one too.

## Instructions

Run the tests:

```bash
dotnet test
```

## Solution notes

- **Attributes are the contract.** `[McpServerTool(Name = ...)]`, `[Description]` on the method and on each parameter
  are what `tools/list` publishes. `get_note` takes `ILogger<StockTools>` as a parameter and the schema still has only
  `orderId`: the SDK resolves services from DI and leaves them out of the schema.
- **Three kinds of failure, three contracts.** `KeyNotFoundException` becomes `IsError = true` with the generic
  `An error occurred invoking 'get_stock'.` (the message stays in the server log). `McpException` keeps its message,
  on purpose, so the model can recover. A tool that does not exist is a JSON-RPC error `-32602`, so `CallToolAsync`
  throws `McpProtocolException`. A successful result has `IsError == null`, not `false`.
- **Prompts and resources.** Prompt and resource types are sealed non-static classes here, because
  `WithPrompts<T>()` and `WithResources<T>()` do not accept static types (the methods can still be static).
- **`ParseSessionMode`** is `Enum.TryParse` with `ignoreCase`, with `Stateless` as the default, the SDK's own default.
- **Session modes on the wire.** `Stateless` gives no `Mcp-Session-Id` and answers a plain `GET /mcp` with 405.
  `Stateful` mints a session for a client pinned to `2025-11-25`, and a default client gets `-32022` on
  `server/discover` and falls back to `2025-11-25` (the test reads `NegotiatedProtocolVersion`).
- **`ToolsForModel`** wraps with `ApprovalRequiredAIFunction`. With it, `FunctionInvokingChatClient` stops with a
  `ToolApprovalRequestContent` and the server never sees the call (the store has no reservation). With the plain
  tool the injected `reserve 999` runs. Tool output is data and tool descriptions are trusted input: the allow-list
  and the approval live in the client.
- **`RunStdioAsync` (TODO-09)** logs with `LogToStandardErrorThreshold = LogLevel.Trace`: on stdio, stdout belongs to
  the protocol.
- **Not covered by tests:** the real stdio child process, the Ollama calls and the SSE body of a multi round-trip
  request. They need a process or a model.
