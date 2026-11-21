using Microsoft.Extensions.AI;

namespace VectorSearchLab;

public static class EmbeddingGuard
{
    public static async Task<float[]> EmbedAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator, string text, int expectedDimensions)
    {
        // TODO-06: A real model happily returns a vector for an empty string, and the in-memory store
        //          never checks the vector size. Guard both: throw ArgumentException for null, empty or
        //          whitespace text BEFORE calling the generator; after generating, throw
        //          InvalidOperationException when the vector length is not expectedDimensions.
        throw new NotImplementedException("Not implemented yet.");
    }
}
