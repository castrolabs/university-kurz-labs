using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace RagLab.Tests;

/// <summary>
/// Deterministic fake: a bag of words hashed (FNV-1a, stable across processes) into a fixed number of
/// buckets and scaled to unit length, like the vectors Ollama returns. Texts that share words get close
/// vectors, texts that share none get cosine 0. It records every batch it receives.
/// Swap it for new OllamaApiClient(new Uri("http://localhost:11434"), "embeddinggemma:300m") to use a real model.
/// </summary>
public sealed partial class HashingEmbeddingGenerator(int dimensions = ChunkRecord.Dimensions)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    public List<string[]> Batches { get; } = [];

    public int Calls => Batches.Count;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var batch = values.ToArray();
        Batches.Add(batch);
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(batch.Select(Embed).ToList()));
    }

    private Embedding<float> Embed(string text)
    {
        var vector = new float[dimensions];
        foreach (Match word in Words().Matches(text.ToLowerInvariant()))
        {
            vector[(int)(Fnv1a(word.Value) % (uint)dimensions)] += 1;
        }

        float norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return new Embedding<float>(vector);
    }

    private static uint Fnv1a(string word)
    {
        uint hash = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes(word))
        {
            hash = (hash ^ b) * 16777619;
        }

        return hash;
    }

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex Words();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
