using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace RagLab;

/// <summary>An in-memory vector store with one collection of chunks. The store embeds the chunk text.</summary>
public sealed class RagStore
{
    private RagStore(VectorStoreCollection<string, ChunkRecord> chunks) => Chunks = chunks;

    public VectorStoreCollection<string, ChunkRecord> Chunks { get; }

    public static async Task<RagStore> CreateAsync(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = generator });
        var chunks = store.GetCollection<string, ChunkRecord>("chunks");
        await chunks.EnsureCollectionExistsAsync();
        return new RagStore(chunks);
    }

    public async Task<int> CountAsync()
    {
        int count = 0;
        await foreach (var _ in Chunks.GetAsync(c => true, top: 10_000))
        {
            count++;
        }

        return count;
    }
}
