using Microsoft.Extensions.AI;

namespace VectorSearchLab;

public static class EmbeddingGuard
{
    public static async Task<float[]> EmbedAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator, string text, int expectedDimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var vector = (await generator.GenerateAsync(text)).Vector;
        if (vector.Length != expectedDimensions)
        {
            throw new InvalidOperationException(
                $"Expected {expectedDimensions} dimensions but the generator returned {vector.Length}.");
        }

        return vector.ToArray();
    }
}
