# Structured Output with Typed Responses

## Goal

Ask a model for a C# type with `GetResponseAsync<T>`, read the JSON schema it generates, see what happens to a reply that is not the JSON you expected, separate parsing from validation with a bounded retry, and avoid the serializer-options and streaming traps.

## Prerequisites

- `IChatClient` (see the Microsoft.Extensions.AI lesson)
- `System.Text.Json` and `System.ComponentModel.DataAnnotations`
- .NET 10 SDK

## Task

The tests use `ScriptedChatClient`, a deterministic `IChatClient` that answers with scripted texts in order (the last one repeats), records every request and, when streamed, splits the reply into chunks of 12 characters. Bad replies (an empty text, a sentence before the JSON, a Markdown fence, `null`) are therefore reproducible. There is no network and no Ollama.

## Instructions

Complete the following TODOs:

- TODO-00: `TicketExtractor.ExtractAsync`: `GetResponseAsync<Ticket>` with temperature 0 and a seed.
- TODO-01: `SchemaInspector.Required<T>`: the `required` list of the schema MEAI generates. Is a `string?` property required?
- TODO-02: `SafeParse.TryExtractAsync`: turn the parsing failures of `Result` into an outcome. Which bad replies fail and which parse silently?
- TODO-03: `Primitives`: an array and an `int` as `T`, and the `data` wrapper around them.
- TODO-04: `Classifier.ClassifyAsync`: an enum as `T`.
- TODO-05: `OrderValidator.Validate`: `[Range]` is not checked by the deserializer.
- TODO-06: `RetryingExtractor.ExtractAsync`: a bounded retry that feeds the error back to the model.
- TODO-07: `JsonText.Parse`: read reply text with the options the schema was built from.
- TODO-08: `StreamingExtractor.ExtractAsync`: there is no streaming `GetResponseAsync<T>`; stream, fold, then parse.

Each failing reply of `ShouldReportEachKindOfBadReplyWithoutThrowing` is a case from the lesson: decide first which ones you expect to throw.

Run the tests until they all pass.

## Running the Lab

```bash
dotnet test
```

## Using Ollama instead of the fake

Nothing in `src/` depends on the provider. To try it against a local model, add
`OllamaSharp` to the test project (`dotnet add tests/StructuredOutputLab.Tests package OllamaSharp --version 5.5.0`)
and replace the fake where a test builds its client:

```csharp
IChatClient model = new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct");
Ticket ticket = await TicketExtractor.ExtractAsync(model, email);
```

Ollama enforces the schema in `format`, so the bad-reply cases will not appear and the
exact-value assertions will not hold. With a model that has no native schema support, try
TODO-09 (`useJsonSchemaResponseFormat: false`).

## Bonus (Optional)

- TODO-09 (optional): `OptionalExtras.ExtractWithPromptSchemaAsync`: `useJsonSchemaResponseFormat: false`, the schema in the prompt.
- TODO-10 (optional): `OptionalExtras.NullIfLiteralNull`: a non-nullable string can come back as the text "null".
