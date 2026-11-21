using Microsoft.Extensions.VectorData;

namespace RagLab;

public sealed record Document(string Id, string Title, string Markdown);

public sealed class ChunkRecord
{
    public const int Dimensions = 256;

    [VectorStoreKey]
    public string Id { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public string DocId { get; set; } = "";

    [VectorStoreData]
    public string Text { get; set; } = "";

    [VectorStoreVector(Dimensions, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding => Text;
}

public sealed record Retrieved(ChunkRecord Chunk, double Score);

public sealed record Answer(string Text, IReadOnlyList<string> Sources);

public sealed record Question(string Text, string ChunkFact);
