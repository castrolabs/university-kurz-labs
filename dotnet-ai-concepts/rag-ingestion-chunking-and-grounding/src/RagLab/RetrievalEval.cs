namespace RagLab;

public static class RetrievalEval
{
    public static async Task<int> HitsAtAsync(RagStore store, IEnumerable<Question> questions, int k)
    {
        // TODO-08: Count the questions whose ChunkFact appears in the text of at least one of the top k
        //          retrieved chunks. Keep a set like this in the repository and rerun it whenever the
        //          chunker, the model or k changes.
        throw new NotImplementedException("Not implemented yet.");
    }
}
