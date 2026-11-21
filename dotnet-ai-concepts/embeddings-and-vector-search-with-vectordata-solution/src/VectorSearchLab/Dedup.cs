namespace VectorSearchLab;

public static class Dedup
{
    public static IReadOnlyList<(int First, int Second)> NearDuplicates(IReadOnlyList<float[]> vectors, float threshold)
    {
        var pairs = new List<(int, int)>();
        for (int i = 0; i < vectors.Count; i++)
        {
            for (int j = i + 1; j < vectors.Count; j++)
            {
                if (VectorMath.Cosine(vectors[i], vectors[j]) >= threshold)
                {
                    pairs.Add((i, j));
                }
            }
        }

        return pairs;
    }
}
