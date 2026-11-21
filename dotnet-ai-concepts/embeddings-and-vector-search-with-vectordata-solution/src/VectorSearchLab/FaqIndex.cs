using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace VectorSearchLab;

public sealed record FaqHit(Faq Faq, double Score);

public sealed class FaqIndex
{
    private readonly VectorStoreCollection<int, Faq> _collection;

    private FaqIndex(VectorStoreCollection<int, Faq> collection) => _collection = collection;

    public static async Task<FaqIndex> BuildAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator, IEnumerable<Faq> faqs)
    {
        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = generator });
        var collection = store.GetCollection<int, Faq>("faq");
        await collection.EnsureCollectionExistsAsync();
        await collection.UpsertAsync(faqs);
        return new FaqIndex(collection);
    }

    public async Task<IReadOnlyList<FaqHit>> SearchAsync(
        string query, int top, double? minScore = null, string? category = null)
    {
        var options = new VectorSearchOptions<Faq>();
        if (category is not null)
        {
            options.Filter = faq => faq.Category == category;
        }

        var hits = new List<FaqHit>();
        await foreach (var result in _collection.SearchAsync(query, top, options))
        {
            if (result.Score is { } score && (minScore is null || score >= minScore))
            {
                hits.Add(new FaqHit(result.Record, score));
            }
        }

        return hits;
    }
}
