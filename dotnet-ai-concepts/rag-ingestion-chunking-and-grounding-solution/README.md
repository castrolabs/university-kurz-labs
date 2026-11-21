# RAG: Ingestion, Chunking and Grounding (Solution)

Official solution. All tests pass, including the two optional ones.

## What to compare with your own code

- `SentenceChunker`: a fact never gets cut in half, and `overlapSentences` repeats the last sentence so a fact at a boundary lives in two chunks. The carried sentence is dropped when it leaves no room for the next one, otherwise the loop would never advance. The sentence regex is naive on purpose (`e.g. ` breaks a sentence).
- `HeadingChunker`: the `Title > Heading` prefix is part of the embedded text, which is why small heading chunks still find their answer. `FixedChunker` with 6-word windows cuts "UDP port 47613" apart: no chunk holds the fact at any k.
- `Ingestor`: deterministic ids (`docId#index`) make an upsert an overwrite; the explicit delete handles what upsert cannot, a chunk that no longer exists. Without it, stale chunks stay searchable.
- `RagAnswerer`: the refusal when nothing passes `minScore` is decided in code, so it costs no model call and cannot be argued away by a prompt. The prompt rule ("reply exactly: Not in the documents.") still carries the refusals for retrieved-but-irrelevant chunks.
- `Citations`: checking that cited ids exist in the sources catches invented citations; `CitedChunkContains` catches a real citation of the wrong chunk.
- `RetrievalEval`: run it whenever the chunker, the model or k changes.
- `RagChatClient` (optional): `DelegatingChatClient` overrides must cover both methods, or the streaming path skips the layer.

Out of scope here: hybrid search, reranking, LLM-judged groundedness metrics, `Microsoft.Extensions.DataIngestion` (preview) and a context-budget trim.

## Running the Lab

```bash
dotnet test
```
