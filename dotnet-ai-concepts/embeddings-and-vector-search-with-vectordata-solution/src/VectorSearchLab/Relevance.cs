using Microsoft.Extensions.VectorData;

namespace VectorSearchLab;

public static class Relevance
{
    /// <summary>Turns a provider score into "higher is more relevant" so one threshold fits every provider.</summary>
    public static double FromScore(double score, string distanceFunction) => distanceFunction switch
    {
        DistanceFunction.CosineSimilarity or DistanceFunction.DotProductSimilarity => score,
        DistanceFunction.CosineDistance => 1 - score,
        _ => throw new NotSupportedException($"No relevance rule for '{distanceFunction}'."),
    };
}
