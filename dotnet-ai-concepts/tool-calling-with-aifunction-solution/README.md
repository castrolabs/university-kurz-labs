# Tool Calling with AIFunction

## Goal

Turn .NET methods into `AIFunction`s, read the schema that is generated, write the tool-calling loop by hand, let `UseFunctionInvocation` run it and bound it (iteration limit, error limit, error detail), authorize and validate before any side effect, and put a human approval in front of a tool.

## Prerequisites

- `IChatClient` and `ChatClientBuilder` (see the Microsoft.Extensions.AI lesson)
- `System.Text.Json` serializer options and JSON Schema basics
- .NET 10 SDK

## Task

The tests use `ScriptedChatClient`, a deterministic `IChatClient` driven by a script: a function from the call index to the response, so a test decides exactly which tool calls the "model" makes. Every response reports 100 input and 10 output tokens, and the client records each request and how many tools came with it. There is no network and no Ollama.

## Instructions

Complete the following TODOs (already done in this solution):

- TODO-00: `ToolCatalog.CreateWeather`: an `AIFunction` from a lambda, with a name and a description of your own.
- TODO-01: `ToolCatalog.CreateForecast`: an `AIFunction` from a method, honoring the serializer options. Read the schema: which parameters are `required`?
- TODO-02: `ManualLoop.RunAsync`: the tool-calling loop by hand, answering each `FunctionCallContent` with a `FunctionResultContent` of the same `CallId`.
- TODO-03: `ToolPipeline.Build`: `MaximumIterationsPerRequest` and `MaximumConsecutiveErrorsPerRequest` from the settings.
- TODO-04: `ToolPipeline.Build`: `IncludeDetailedErrors` from the settings. What does the model see with it on and off?
- TODO-05: `ToolPipeline.Build`: a `FunctionInvoker` that refuses the blocked tools and runs the others.
- TODO-06: `ToolResponses.EndedWithUnansweredCall`: recognize a loop that stopped at its limit, with no exception.
- TODO-07: `BankTools.Transfer`: validate what the model sent before any side effect and return a sentence the model can use.
- TODO-08: `ApprovalFlow.RunAsync`: `ApprovalRequiredAIFunction`, the approval request and the approval response.

Read the iteration-limit test closely: nothing throws, the last request carries no tools, and the response still ends in a function call.

Run the tests until they all pass.

## Running the Lab

```bash
dotnet test
```

## Using Ollama instead of the fake

Nothing in `src/` depends on the provider. To try it against a local model, add
`OllamaSharp` to the test project (`dotnet add tests/ToolCallingLab.Tests package OllamaSharp --version 5.5.0`)
and replace the fake where a test builds its client:

```csharp
IChatClient model = new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct");
var client = ToolPipeline.Build(model);
var response = await client.GetResponseAsync("What is the weather in Lisbon?", new ChatOptions { Tools = [ToolCatalog.CreateWeather()] });
```

The model decides the calls, so the tests that count model calls will not hold.
`ChatOptions.ToolMode` is a request: OllamaSharp 5.5.0 ignores it, so to withhold tools,
leave them out of `Tools`.

## Bonus (Optional)

- TODO-09 (optional): apply `AllowConcurrentInvocation` (`ToolLoopSettings.Concurrent`) and write a tool that records how many calls run at the same time.
- TODO-10 (optional): `BankTools.TransferOnce`: deduplicate a side effect with a business key, since the `CallId` changes on every call.
