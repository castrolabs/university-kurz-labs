# Testing MCP Servers In Process and with the Inspector

## Goal

The stock MCP server in `src/StockMcp` is already fully implemented: two tools (`get_stock`, `reserve`), one of them
failing with an ordinary exception and the other with an `McpException`, served over Streamable HTTP at `/mcp`. Your
job is to write the MSTest suite that pins its public contract and runs in about one second: the real server and the
real `McpClient` joined by two in-memory pipes, the same tools over HTTP with `WebApplicationFactory`, timeouts and the
`server/discover` fallback driven by a `FakeTimeProvider`, and the `tools/list` output saved as a snapshot so that an
accidental schema change or a rug pull fails the build. No model, no Docker, no port.

## Prerequisites

- MCP servers and clients with the C# SDK (the concept and its lab)
- Integration Testing with WebApplicationFactory and Controlling Time with TimeProvider (the concepts and their labs)
- Microsoft.Testing.Platform and `dotnet test` (`global.json` next to the solution selects the runner)
- .NET 10 SDK

## Task

Read `src/StockMcp` (`StockTools`, `TamperedStockTools`, `Program.cs`) and `tests/StockMcp.Tests/ScriptedPeer.cs`,
which is given. Then open the test project. Every test method is declared with a hint and fails with
`TODO-NN: write this test` until you write it, and two helpers (`InProcessMcp`, `Snapshot`) are stubs that throw
`NotImplementedException`.

Three mistakes are easy to make here, and each one gives you a green suite that proves less than it seems:

- asserting `IsError == false` for success, when the SDK leaves it `null`;
- treating "the tool failed" as one thing: an ordinary exception, an `McpException` and an unknown tool are three
  different contracts;
- advancing a fake clock before the client has armed its timer, or with `Thread.Sleep`: read the first request from
  the peer first.

## Instructions

Complete the following TODOs in `tests/StockMcp.Tests`:

- TODO-00: `InProcessMcp`: the server on a generic host and the client, joined by two `Pipe`s.
- TODO-01: `ToolsListContractTests`: the tools and their descriptions, the parameter schema, `IsError` on success.
- TODO-02: `ToolsListContractTests`: the three failure contracts (ordinary exception, `McpException`, unknown tool).
- TODO-03: `HttpTransportTests`: the same server through `WebApplicationFactory<Program>`: no session, `GET` refused.
- TODO-04: `ClientTimeoutTests`: the initialization timeout and the discover fallback, with `FakeTimeProvider` and `ScriptedPeer`.
- TODO-05: `Snapshot.Verify`: compare with a checked-in `.verified.txt`, write a `.received.txt` and fail otherwise.
- TODO-06: `Snapshot.RenderTools` and the two snapshot tests (in process and over HTTP, one shared file). Approve the
  first `.received.txt` yourself, after reading it.
- TODO-07: the rug pull: `TamperedStockTools` must change the rendered contract, and the diff must show how.

Run the tests until they all pass.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## The Inspector (docs only)

The MCP Inspector is the official tool to poke at a server by hand, and it is not part of the test suite: it needs
Node and downloads a package, and the suite must stay offline. With the server running (for example
`dotnet run --project src/StockMcp --urls http://127.0.0.1:6020`), its CLI mode works as a smoke test in a script:

```bash
npx @modelcontextprotocol/inspector@2.16.0 --cli http://127.0.0.1:6020/mcp --method tools/list
npx @modelcontextprotocol/inspector@2.16.0 --cli http://127.0.0.1:6020/mcp --method tools/call --tool-name get_stock --tool-arg sku=SKU-1
```

Try `reserve` with `quantity=0` and a tool that does not exist, and compare what the Inspector prints with what your
C# tests assert: the Inspector checks the tool list before it calls, so an unknown tool never reaches the server and
the `-32602` your test sees is not what the Inspector shows. Pin the version in any script that parses its output.
The suite is the repeatable check; the Inspector answers "what does this server say right now".

## Bonus (Optional)

- TODO-08 (optional): Mutation check. Break the production code on purpose, one change at a time: edit the description of
  `reserve`, throw `InvalidOperationException` instead of `McpException` for quantity 0, switch the session mode to
  `Stateful`, remove `MapMcp`. Each change should turn at least one test red. Then find a change that stays green
  (for example, making `reserve` refuse quantity 1 as well) and write the test that catches it.
