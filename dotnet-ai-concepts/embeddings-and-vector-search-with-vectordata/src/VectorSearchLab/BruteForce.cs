namespace VectorSearchLab;

public static class BruteForce
{
    public static IReadOnlyList<int> TopK(float[] query, IReadOnlyList<float[]> candidates, int k)
    {
        // TODO-10 (optional): Rank the candidate indexes by cosine similarity to the query (best first,
        //          lower index wins a tie) and return the first k. This is what an in-process store does:
        //          a full scan.
        throw new NotImplementedException("Not implemented yet.");
    }
}
