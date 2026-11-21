using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;

namespace VectorSearchLab;

public static class EmbeddingPipeline
{
    public static IEmbeddingGenerator<string, Embedding<float>> Cached(
        IEmbeddingGenerator<string, Embedding<float>> generator, IDistributedCache cache) =>
        new EmbeddingGeneratorBuilder<string, Embedding<float>>(generator)
            .UseDistributedCache(cache)
            .Build();
}
