# RAG: Ingestion, Chunking and Grounding

## Goal

Build the whole retrieval-augmented generation loop around a model you can fake: chunk documents at sentence and heading boundaries, ingest them idempotently into a vector store, retrieve, build a prompt that carries the sources, answer with citations, refuse when nothing relevant was found, verify the citations from the outside and measure retrieval with a regression set.

## Prerequisites

- `IChatClient` (see the Microsoft.Extensions.AI lesson) and `IEmbeddingGenerator`
- A vector record with `[VectorStoreKey]`, `[VectorStoreData]` and `[VectorStoreVector]` (see the embeddings lesson)
- .NET 10 SDK

## Task

The tests use two deterministic fakes. `HashingEmbeddingGenerator` turns a bag of words into a unit vector (texts that share words are close, texts that share none score 0). `ScriptedChatClient` answers with whatever a script returns and records every request, so a test can assert what the model was sent and how many times it was called. There is no network and no Ollama.

`RagStore` (an in-memory store whose generator embeds the chunk text), `FixedChunker` (word windows, the baseline to beat) and the models are given. Word counts stand in for tokens: a real tokenizer counts 4% to 15% more.

Packages: `Microsoft.Extensions.AI` 10.11.0, `Microsoft.Extensions.VectorData.Abstractions` 10.11.0, `CommunityToolkit.VectorData.InMemory` 1.0.1, `Microsoft.Extensions.Caching.Memory` 10.0.14.

## Instructions

Complete the following TODOs:

- TODO-00: `SentenceChunker.Chunk`: whole sentences up to a word budget, with sentence overlap.
- TODO-01: `HeadingChunker.Chunk`: one chunk per `##` section, prefixed with `Title > Heading`.
- TODO-02: `Ingestor.IngestAsync`: deterministic chunk ids, upsert, delete the chunks a new version no longer has.
- TODO-03: `Retriever.SearchAsync`: the k best chunks for a question, with scores.
- TODO-04: `PromptBuilder.Build`: the rules in a system message, the cited sources and the question in a user message.
- TODO-05: `RagAnswerer.AskAsync`: retrieve, filter by score, refuse WITHOUT calling the model when nothing is relevant.
- TODO-06: `Citations.Check`: the ids cited in square brackets and the ones that are not among the sources.
- TODO-07: `Citations.CitedChunkContains`: does a chunk the answer cites really hold the fact?
- TODO-08: `RetrievalEval.HitsAtAsync`: the regression-set metric, how many questions find their chunk in the top k.

Run the tests until they all pass. The last test compares heading chunks with 6-word windows over the same questions.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## Using Ollama instead of the fake

Nothing in `src/` depends on the provider. Add `OllamaSharp` to the test project (`dotnet add tests/RagLab.Tests package OllamaSharp --version 5.5.0`), `ollama pull embeddinggemma:300m` and `ollama pull qwen3:4b-instruct`, and build both clients with `new OllamaApiClient(new Uri("http://localhost:11434"), "<model>")`. The embedding model returns 768 dimensions (change `ChunkRecord.Dimensions`), scores and answers are no longer exact, and the refusal threshold in `RagAnswerer` needs tuning: unrelated sentences score 0.18 to 0.32 there, not 0.

## Bonus (Optional)

The two tests for these tasks pass until you start them, then they check your work.

- TODO-09 (optional): `RagChatClient.GetResponseAsync`: keep the app `IChatClient`-shaped, a layer that retrieves for the last user message and reports the source ids in `AdditionalProperties`.
- TODO-10 (optional): `RagChatClient.GetStreamingResponseAsync`: the same grounding for streaming.
