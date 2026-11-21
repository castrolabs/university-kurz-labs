# Embeddings and Vector Search with Microsoft.Extensions.VectorData

## Goal

Compute cosine similarity and normalize vectors by hand, declare a vector record for `Microsoft.Extensions.VectorData`, search an in-memory store with a filter and a score threshold, make scores comparable across providers, guard the two silent failures of embedding (blank text, wrong vector size) and cache embeddings.

## Prerequisites

- `IEmbeddingGenerator<string, Embedding<float>>` and `EmbeddingGeneratorBuilder` (see the embeddings lesson)
- Attributes `[VectorStoreKey]`, `[VectorStoreData]` and `[VectorStoreVector]`
- .NET 10 SDK

## Task

The tests use `HashingEmbeddingGenerator`, a deterministic fake: a bag of words hashed into 256 buckets and scaled to unit length. Texts that share words get close vectors, texts that share none score exactly 0, and the fake records every batch it receives. There is no network and no Ollama.

Packages: `Microsoft.Extensions.AI` 10.11.0, `Microsoft.Extensions.VectorData.Abstractions` 10.11.0, `CommunityToolkit.VectorData.InMemory` 1.0.1, `Microsoft.Extensions.Caching.Memory` 10.0.14.

## Instructions

Complete the following TODOs:

- TODO-00: `VectorMath.Cosine`: dot over the product of the norms, `ArgumentException` for different lengths, 0 (not NaN) for a zero vector.
- TODO-01: `VectorMath.Normalize`: a new array of length 1 with the same direction.
- TODO-02: `Faq.Embedding`: declare the vector property with its size and an explicit `DistanceFunction`.
- TODO-03: `FaqIndex.BuildAsync`: an `InMemoryVectorStore` that owns the generator, the collection, upsert.
- TODO-04: `FaqIndex.SearchAsync`: search by text, filter by category with a LINQ expression, drop results under `minScore`.
- TODO-05: `Relevance.FromScore`: one "higher is better" scale for similarity and distance scores.
- TODO-06: `EmbeddingGuard.EmbedAsync`: reject blank text before calling the model, reject a vector of the wrong size.
- TODO-07: `EmbeddingPipeline.Cached`: `UseDistributedCache` around the generator. Read what the mixed-batch test expects.
- TODO-08: `Dedup.NearDuplicates`: index pairs whose cosine reaches a threshold.

Run the tests until they all pass.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## Using Ollama instead of the fake

Nothing in `src/` depends on the provider. To try a real model, add `OllamaSharp` to the test project (`dotnet add tests/VectorSearchLab.Tests package OllamaSharp --version 5.5.0`), `ollama pull embeddinggemma:300m`, and build the generator with `new OllamaApiClient(new Uri("http://localhost:11434"), "embeddinggemma:300m")`. That model returns 768 dimensions, so change `Faq.Dimensions`, and the exact-score assertions will not hold: unrelated sentences score 0.18 to 0.32 there, not 0.

## Bonus (Optional)

The two tests for these tasks pass until you start them, then they check your work.

- TODO-09 (optional): `TaskPrefixes`: the query and document prefixes of the embeddinggemma model card. A threshold tuned with one convention is wrong under the other.
- TODO-10 (optional): `BruteForce.TopK`: rank candidates by cosine yourself and compare with what the vector store returns.
