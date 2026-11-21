using Microsoft.Extensions.VectorData;

namespace VectorSearchLab;

public sealed class Faq
{
    public const int Dimensions = 256;

    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string Category { get; set; } = "";

    [VectorStoreData]
    public string Text { get; set; } = "";

    [VectorStoreVector(Dimensions, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding => Text;
}
