namespace RagLab;

public static class Ingestor
{
    /// <summary>Idempotent: same document, same ids, so running it twice overwrites; chunks the new version no longer has are deleted.</summary>
    public static async Task IngestAsync(RagStore store, Document document, Func<Document, IReadOnlyList<string>> chunker)
    {
        var records = chunker(document)
            .Select((text, index) => new ChunkRecord { Id = $"{document.Id}#{index}", DocId = document.Id, Text = text })
            .ToList();
        await store.Chunks.UpsertAsync(records);

        var keep = records.Select(r => r.Id).ToHashSet();
        var stale = new List<string>();
        await foreach (var old in store.Chunks.GetAsync(r => r.DocId == document.Id, top: 1_000))
        {
            if (!keep.Contains(old.Id))
            {
                stale.Add(old.Id);
            }
        }

        await store.Chunks.DeleteAsync(stale);
    }
}
