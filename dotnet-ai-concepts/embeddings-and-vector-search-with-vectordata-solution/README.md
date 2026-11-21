# Embeddings and Vector Search with Microsoft.Extensions.VectorData (Solution)

Official solution. All tests pass, including the two optional ones.

## What to compare with your own code

- `VectorMath`: `TensorPrimitives.Norm`, `Dot` and `Divide` do the arithmetic. Cosine of a zero vector would be 0/0, so it is decided explicitly. With unit vectors (what Ollama returns, and what the fake returns) the dot product alone equals the cosine and is cheaper.
- `Faq.Embedding`: the `[VectorStoreVector]` attribute carries the size and `DistanceFunction.CosineSimilarity`. The in-memory store defaults to cosine similarity, but SQLite returns cosine distance (`1 - similarity`, lower is better), so a hard-coded `Score > 0.5` inverts when the provider changes. Pinning the function makes a swap fail loudly.
- `FaqIndex.SearchAsync`: the filter is a LINQ expression each provider translates. It narrows the candidates but does not remove weak matches, so the score threshold stays.
- `Relevance.FromScore`: scores are comparable only inside one provider and one distance function; this is the single place that knows the meaning.
- `EmbeddingGuard`: the in-memory store never checks the vector size (SQLite does) and real models return a valid-looking vector for an empty string, so the checks live in your code.
- `EmbeddingPipeline.Cached`: the cache works per item, so a mixed batch sends only the missing texts to the model. One cache per model: vectors from two models are not comparable.
- `TaskPrefixes` (optional): prefixes change the scale of the scores more than the ranking.
- `BruteForce` (optional): a full scan; both in-process stores scale linearly with the number of records.

## Running the Lab

```bash
dotnet test
```
