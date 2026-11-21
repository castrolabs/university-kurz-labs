using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;

namespace VectorSearchLab;

public static class EmbeddingPipeline
{
    public static IEmbeddingGenerator<string, Embedding<float>> Cached(
        IEmbeddingGenerator<string, Embedding<float>> generator, IDistributedCache cache)
    {
        // TODO-07: Wrap the generator with EmbeddingGeneratorBuilder and UseDistributedCache(cache).
        //          Then check in the tests what happens to a batch that mixes cached and new texts.
        return generator;
    }
}
