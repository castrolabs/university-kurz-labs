namespace VectorSearchLab;

public static class Dedup
{
    public static IReadOnlyList<(int First, int Second)> NearDuplicates(IReadOnlyList<float[]> vectors, float threshold)
    {
        // TODO-08: Return every pair of indexes (First < Second, each pair once) whose cosine similarity
        //          is at least the threshold. Reuse VectorMath.Cosine.
        throw new NotImplementedException("Not implemented yet.");
    }
}
