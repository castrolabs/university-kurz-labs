namespace RagLab;

public static class RetrievalEval
{
    /// <summary>How many questions have the chunk holding the answer among the top k results.</summary>
    public static async Task<int> HitsAtAsync(RagStore store, IEnumerable<Question> questions, int k)
    {
        int hits = 0;
        foreach (var question in questions)
        {
            var retrieved = await Retriever.SearchAsync(store, question.Text, k);
            if (retrieved.Any(r => r.Chunk.Text.Contains(question.ChunkFact)))
            {
                hits++;
            }
        }

        return hits;
    }
}
