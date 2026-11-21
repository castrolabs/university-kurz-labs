using Microsoft.Extensions.VectorData;

namespace VectorSearchLab;

public static class Relevance
{
    public static double FromScore(double score, string distanceFunction)
    {
        // TODO-05: Turn a provider score into "higher is more relevant". Similarity functions
        //          (CosineSimilarity, DotProductSimilarity) are already like that; CosineDistance is
        //          1 - similarity, so flip it. Any other function: throw NotSupportedException.
        throw new NotImplementedException("Not implemented yet.");
    }
}
