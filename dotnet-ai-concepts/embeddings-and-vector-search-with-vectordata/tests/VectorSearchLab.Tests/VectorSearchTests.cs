using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;

namespace VectorSearchLab.Tests;

public class VectorSearchTests
{
    private static readonly Faq[] Faqs =
    [
        new() { Id = 1, Category = "account", Text = "Reset your password from the account settings page" },
        new() { Id = 2, Category = "account", Text = "Passwords must contain at least twelve characters" },
        new() { Id = 3, Category = "billing", Text = "Invoices are sent on the first day of each month" },
        new() { Id = 4, Category = "billing", Text = "Change the credit card used for billing in the payment settings" },
        new() { Id = 5, Category = "shipping", Text = "Orders ship within two business days" },
        new() { Id = 6, Category = "shipping", Text = "Track your order with the tracking number in the shipping email" },
    ];

    private static IDistributedCache NewCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static async Task<float[]> Embed(HashingEmbeddingGenerator generator, string text) =>
        (await generator.GenerateAsync(text)).Vector.ToArray();

    [Fact]
    public void Cosine_is_one_for_equal_zero_for_orthogonal_and_minus_one_for_opposite_vectors()
    {
        Assert.Equal(1f, VectorMath.Cosine([1, 2, 3], [2, 4, 6]), 5);
        Assert.Equal(0f, VectorMath.Cosine([1, 0], [0, 1]), 5);
        Assert.Equal(-1f, VectorMath.Cosine([1, 2], [-1, -2]), 5);
    }

    [Fact]
    public void Cosine_rejects_different_lengths_and_treats_a_zero_vector_as_unrelated()
    {
        Assert.Throws<ArgumentException>(() => VectorMath.Cosine([1, 2], [1, 2, 3]));
        Assert.Equal(0f, VectorMath.Cosine([0, 0], [1, 2]));
    }

    [Fact]
    public void Normalize_returns_a_unit_vector_with_the_same_direction_and_keeps_zero_as_zero()
    {
        float[] unit = VectorMath.Normalize([3, 4]);

        Assert.Equal(0.6f, unit[0], 5);
        Assert.Equal(0.8f, unit[1], 5);
        Assert.Equal([0f, 0f], VectorMath.Normalize([0, 0]));
    }

    [Fact]
    public async Task Search_ranks_the_matching_faq_first()
    {
        var index = await FaqIndex.BuildAsync(new HashingEmbeddingGenerator(), Faqs);

        var hits = await index.SearchAsync("reset password", top: 3);

        Assert.Equal(3, hits.Count);
        Assert.Equal(1, hits[0].Faq.Id);
        Assert.True(hits[0].Score > hits[1].Score);
    }

    [Fact]
    public async Task Search_filter_keeps_only_the_requested_category_even_when_a_better_match_exists_elsewhere()
    {
        var index = await FaqIndex.BuildAsync(new HashingEmbeddingGenerator(), Faqs);

        var hits = await index.SearchAsync("reset password settings", top: 3, category: "billing");

        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal("billing", h.Faq.Category));
        Assert.Equal(4, hits[0].Faq.Id);
    }

    [Fact]
    public async Task Search_min_score_drops_weak_matches()
    {
        var index = await FaqIndex.BuildAsync(new HashingEmbeddingGenerator(), Faqs);

        var all = await index.SearchAsync("reset password", top: 6);
        var strong = await index.SearchAsync("reset password", top: 6, minScore: 0.3);

        Assert.Equal(6, all.Count);
        Assert.Equal([1], strong.Select(h => h.Faq.Id));
    }

    [Fact]
    public void Relevance_means_higher_is_better_for_similarity_and_distance_scores_alike()
    {
        Assert.Equal(0.8, Relevance.FromScore(0.8, DistanceFunction.CosineSimilarity), 5);
        Assert.Equal(0.8, Relevance.FromScore(0.2, DistanceFunction.CosineDistance), 5);
        Assert.Equal(1.0, Relevance.FromScore(0.0, DistanceFunction.CosineDistance), 5);
        Assert.Throws<NotSupportedException>(() => Relevance.FromScore(1.2, DistanceFunction.EuclideanDistance));
    }

    [Fact]
    public async Task Guard_rejects_blank_text_before_calling_the_generator()
    {
        var generator = new HashingEmbeddingGenerator();

        await Assert.ThrowsAsync<ArgumentException>(() => EmbeddingGuard.EmbedAsync(generator, "   ", Faq.Dimensions));

        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task Guard_fails_when_the_vector_size_is_not_the_one_the_store_expects()
    {
        var wrongSize = new HashingEmbeddingGenerator(dimensions: 8);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => EmbeddingGuard.EmbedAsync(wrongSize, "reset password", Faq.Dimensions));

        var vector = await EmbeddingGuard.EmbedAsync(new HashingEmbeddingGenerator(), "reset password", Faq.Dimensions);
        Assert.Equal(Faq.Dimensions, vector.Length);
    }

    [Fact]
    public async Task Cache_serves_the_same_text_without_calling_the_generator_again()
    {
        var fake = new HashingEmbeddingGenerator();
        var cached = EmbeddingPipeline.Cached(fake, NewCache());

        var first = await cached.GenerateAsync("reset password");
        var second = await cached.GenerateAsync("reset password");

        Assert.Equal(1, fake.Calls);
        Assert.Equal(first.Vector.ToArray(), second.Vector.ToArray());
    }

    [Fact]
    public async Task Cache_works_per_item_so_a_mixed_batch_only_sends_the_missing_text()
    {
        var fake = new HashingEmbeddingGenerator();
        var cached = EmbeddingPipeline.Cached(fake, NewCache());
        await cached.GenerateAsync("reset password");

        var batch = await cached.GenerateAsync(["reset password", "invoice date"]);

        Assert.Equal(2, batch.Count);
        Assert.Equal(["invoice date"], fake.Batches.Last());
    }

    [Fact]
    public async Task Dedup_flags_texts_whose_vectors_are_closer_than_the_threshold()
    {
        var fake = new HashingEmbeddingGenerator();
        float[][] vectors =
        [
            await Embed(fake, "reset your password"),
            await Embed(fake, "reset your password please"),
            await Embed(fake, "orders ship in two days"),
        ];

        Assert.Equal([(0, 1)], Dedup.NearDuplicates(vectors, threshold: 0.8f));
        Assert.Empty(Dedup.NearDuplicates(vectors, threshold: 0.95f));
    }

    // Optional tasks: these two tests pass until you start the optional TODO, then they check your work.

    [Fact]
    public void Optional_task_prefixes_follow_the_embeddinggemma_convention()
    {
        string query;
        try
        {
            query = TaskPrefixes.Query("how do I reset my password");
        }
        catch (NotImplementedException)
        {
            return;
        }

        Assert.Equal("task: search result | query: how do I reset my password", query);
        Assert.Equal("title: none | text: Reset it.", TaskPrefixes.Document(null, "Reset it."));
        Assert.Equal("title: Passwords | text: Reset it.", TaskPrefixes.Document("Passwords", "Reset it."));
    }

    [Fact]
    public async Task Optional_task_brute_force_top_k_agrees_with_the_vector_store()
    {
        var fake = new HashingEmbeddingGenerator();
        var vectors = new List<float[]>();
        foreach (var faq in Faqs)
        {
            vectors.Add(await Embed(fake, faq.Text));
        }

        IReadOnlyList<int> ranked;
        try
        {
            ranked = BruteForce.TopK(await Embed(fake, "reset password"), vectors, 3);
        }
        catch (NotImplementedException)
        {
            return;
        }

        var index = await FaqIndex.BuildAsync(fake, Faqs);
        var hits = await index.SearchAsync("reset password", top: 3);
        Assert.Equal(hits.Select(h => h.Faq.Id), ranked.Select(i => Faqs[i].Id));
    }
}
