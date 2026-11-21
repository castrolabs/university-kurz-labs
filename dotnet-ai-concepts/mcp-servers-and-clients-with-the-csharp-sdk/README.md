# MCP Servers and Clients with the C# SDK

## Goal

Build the stock MCP server with the `ModelContextProtocol` 2.4.0 SDK (tools, a prompt, a resource, errors, session
modes), write the small client-side helpers that turn its tools into something a model may use safely, and then read
the contract from the outside with a real `McpClient`: what `tools/list` says, how a failure looks to the client, what
the two HTTP session modes put on the wire.

## Prerequisites

- Dependency injection and the generic host
- `IChatClient`, `AIFunction` and `UseFunctionInvocation` (see the Microsoft.Extensions.AI lesson)
- Minimal APIs and `WebApplicationFactory`
- .NET 10 SDK

## Task

`src/StockMcp` is a server with three tools (`get_stock`, `reserve`, `get_note`), one prompt, one resource and a
`Program.cs` for Streamable HTTP. The bodies and the attributes are missing. The tests start the real server and the
real `McpClient` joined by two in-memory pipes (`InProcessMcp`, no process and no port), and the HTTP tests use
`WebApplicationFactory<Program>` (the in-memory `TestServer`, no port either). There is no model and no network:
`ScriptedChatClient` plays a model that has read the customer note and always asks to reserve 999 units.

Read the tests first. They show what a client sees, and the failure messages tell you what is still missing.

## Instructions

Complete the following TODOs:

- TODO-00: `StockTools.GetStock`: expose it as `get_stock`, with the descriptions the model reads, and an exception for an unknown SKU.
- TODO-01: `StockTools.Reserve`: expose it as `reserve`, and refuse a non-positive quantity with the exception whose message reaches the client.
- TODO-02: `StockTools.GetNote`: a static tool with an injected `ILogger` that must not become an argument.
- TODO-03: `StockPrompts` and `StockResources`: the `restock_plan` prompt and the `stock://policy` resource.
- TODO-04: `StockServer.AddStockCapabilities`: register the three types on the builder.
- TODO-05: `StockServer.ParseSessionMode`: the configuration value to `HttpServerSessionMode`, `Stateless` by default.
- TODO-06: `Program.cs`: the HTTP transport with that session mode, and `MapMcp("/mcp")`.
- TODO-07: `StockClient.TextOf`: the text of a tool result.
- TODO-08: `StockClient.ToolsForModel`: an allow-list of tools, the risky ones wrapped so that a human must approve them.

Run the tests until they all pass. Pay attention to three tests that pin behavior you may not expect:

- an ordinary exception and an `McpException` reach the client differently, and a tool that does not exist is not a
  tool result at all;
- the same two tests of HTTP session modes show which headers and which protocol revision each mode gives a default
  client and one pinned to `2025-11-25`;
- the last tool test shows what `UseFunctionInvocation()` does with an injected instruction when the tool is plain and
  when it is wrapped.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

All 14 tests fail except the protocol-version one until the TODOs are done (that one needs no tool).

## Bonus (Optional)

- TODO-09 (optional): `StockServer.RunStdioAsync`: the same server over stdio. Nothing in the test suite calls it,
  because a stdio server is a child process. Try it by hand: put a stray `Console.WriteLine("starting")` before
  `RunAsync` and see what the client logs.
- Write a test with a `DelegatingHandler` around the `HttpClient` that records every request, and print the
  `MCP-Protocol-Version`, `Mcp-Method` and `Mcp-Name` headers of a `tools/call`.
