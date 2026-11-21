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
        // TODO-03: Create an InMemoryVectorStore that owns the generator (VectorStoreOptions.EmbeddingGenerator),
        //          get the "faq" collection (key int, record Faq), make sure it exists and upsert the faqs.
        //          Return a FaqIndex over that collection.
        throw new NotImplementedException("Not implemented yet.");
    }

    public async Task<IReadOnlyList<FaqHit>> SearchAsync(
        string query, int top, double? minScore = null, string? category = null)
    {
        // TODO-04: Search the collection with the query TEXT (the store embeds it) and return the top results
        //          as FaqHit. When category is given, filter with VectorSearchOptions<Faq>.Filter
        //          (a LINQ expression). When minScore is given, drop results scoring below it:
        //          a filter keeps weak matches, so the threshold still matters.
        throw new NotImplementedException("Not implemented yet.");
    }
}
