namespace VectorSearchLab;

public static class BruteForce
{
    public static IReadOnlyList<int> TopK(float[] query, IReadOnlyList<float[]> candidates, int k) =>
        candidates
            .Select((vector, index) => (index, score: VectorMath.Cosine(query, vector)))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Take(k)
            .Select(x => x.index)
            .ToList();
}
