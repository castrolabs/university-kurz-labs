# IChatClient and the Middleware Pipeline

## Goal

Keep a conversation on top of a stateless `IChatClient`, build a `ChatClientBuilder` pipeline from `DelegatingChatClient` layers and a shared `Use` delegate, see why the first `Use` is the outermost layer and why a layer that overrides only one method misses the streaming call, read a stream back into one response, decide where a counting layer sits relative to `UseDistributedCache`, and register the result in dependency injection.

## Prerequisites

- C# async iterators (`await foreach`, `IAsyncEnumerable<T>`)
- Decorators and the delegating-handler idea (`HttpMessageHandler`)
- `Microsoft.Extensions.AI` 10.11.0 and `IDistributedCache`
- .NET 10 SDK

## Task

The tests use `ScriptedChatClient`, a deterministic `IChatClient` that answers "Paris", reports 17 input and 2 output tokens, streams its answer in two chunks and records every request it receives. A bare variant has no model id, no metadata and no usage, and a cut-off variant ends its stream without a finish reason. There is no network and no Ollama.

## Instructions

Complete the following TODOs (already done in this solution):

- TODO-00: `Conversation.AskAsync`: keep the history and resend all of it on every turn (the client forgets everything).
- TODO-01: `TracingChatClient.GetResponseAsync`: log "enter" and "exit" around the call below, even when it throws.
- TODO-02: `TracingChatClient.GetStreamingResponseAsync`: the same pair around the whole stream. A layer that forwards the method it does not override never sees it.
- TODO-03: `Pipelines.BuildTraced`: layers A and B (`TracingChatClient`) and C (one delegate in the shared `Use` overload). The first `Use` is the outermost layer.
- TODO-04: `StreamReader.ReadAsync`: forward each chunk of text, fold the updates into one `ChatResponse` and say whether the stream really finished.
- TODO-05: `Pipelines.BuildCached`: `UseDistributedCache` and a counting layer, placed outside or inside the cache depending on the flag.
- TODO-06: `ModelRegistration.AddModels`: `AddChatClient(...).UseDistributedCache()` for the default client and `AddKeyedChatClient` for the fake.
- TODO-07: `ProviderInfo.Describe`: read `ChatClientMetadata` through the pipeline with `GetService`, or answer "unknown".
- TODO-08: `ProviderInfo.SayHelloAsync`: one method that runs against any provider and tolerates a missing model id or usage.

Read the cache tests together: the same two identical prompts reach the model once, but a layer outside the cache still sees both.

Run the tests until they all pass.

## Running the Lab

```bash
dotnet test
```

## Using Ollama instead of the fake

Nothing in `src/` depends on the provider. To try it against a local model, add
`OllamaSharp` to the test project (`dotnet add tests/MeaiPipelineLab.Tests package OllamaSharp --version 5.5.0`)
and replace the fake where a test builds its client:

```csharp
IChatClient model = new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct");
var chat = new Conversation(model, "Answer in one short sentence.");
```

The answers are then real, so the exact-text assertions will not hold, and a model
may add its own `FinishReason` and usage that the fake does not.

## Bonus (Optional)

- TODO-09 (optional): `Pipelines.BuildLogged`: add `UseLogging`. At which `LogLevel` does it write the call, and where does the full prompt show up?
