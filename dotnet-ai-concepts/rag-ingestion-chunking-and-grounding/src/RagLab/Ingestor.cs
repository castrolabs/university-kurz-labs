namespace RagLab;

public static class Ingestor
{
    public static async Task IngestAsync(RagStore store, Document document, Func<Document, IReadOnlyList<string>> chunker)
    {
        // TODO-02: Make ingestion idempotent. Give each chunk a deterministic id "{document.Id}#{index}" (and
        //          DocId), upsert them, then delete the chunks of the same document that the new version no
        //          longer has (find them with Chunks.GetAsync(r => r.DocId == ..., top: 1_000)).
        //          Running it twice must leave the same chunks; a shorter document must leave no stale chunk.
        throw new NotImplementedException("Not implemented yet.");
    }
}
