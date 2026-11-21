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

    // TODO-02: Mark this property as the vector: the store embeds this string with its generator.
    //          Declare the size (Dimensions) and pin the DistanceFunction to cosine similarity
    //          instead of relying on the provider default (scores differ in meaning between providers).
    public string Embedding => Text;
}
