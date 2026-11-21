namespace RagLab;

public static class Retriever
{
    public static async Task<IReadOnlyList<Retrieved>> SearchAsync(RagStore store, string question, int k)
    {
        var hits = new List<Retrieved>();
        await foreach (var result in store.Chunks.SearchAsync(question, k))
        {
            hits.Add(new Retrieved(result.Record, result.Score ?? 0));
        }

        return hits;
    }
}
