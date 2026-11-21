using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeaiPipelineLab.Tests;

public sealed class PipelineTests
{
    private static IDistributedCache NewCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    /// <summary>The trap of the lesson: it overrides only GetResponseAsync, so streaming slips through it.</summary>
    private sealed class LeakyTracer(IChatClient inner, string name, List<string> log) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            log.Add($"{name} enter");
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            log.Add($"{name} exit");
            return response;
        }
    }

    // --- the client does not remember ---

    [Fact]
    public async Task ShouldResendTheWholeHistoryOnEveryTurn()
    {
        var model = new ScriptedChatClient("Hello Ana!");
        var chat = new Conversation(model, "Answer in one short sentence.");

        await chat.AskAsync("My name is Ana.");
        await chat.AskAsync("What is my name?");

        Assert.Equal([2, 4], model.Requests.Select(r => r.Count));
        Assert.Equal(
            [ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User],
            model.Requests[1].Select(m => m.Role));
        Assert.Equal(5, chat.History.Count);   // system, user, assistant, user, assistant

        await new Conversation(model).AskAsync("What is my name?");   // a new conversation knows nothing
        Assert.Single(model.Requests[^1]);
    }

    // --- the pipeline and its order ---

    [Fact]
    public async Task ShouldRunTheFirstUseOutermostForGetResponse()
    {
        List<string> log = [];
        var client = Pipelines.BuildTraced(new ScriptedChatClient(), log);

        await client.GetResponseAsync("hello");

        Assert.Equal(["A enter", "B enter", "C enter", "C exit", "B exit", "A exit"], log);
    }

    [Fact]
    public async Task ShouldTraceStreamingToo()
    {
        List<string> log = [];
        var client = Pipelines.BuildTraced(new ScriptedChatClient(), log);

        await foreach (var _ in client.GetStreamingResponseAsync("hello")) { }

        Assert.Equal("A enter", log[0]);
        Assert.Equal("B enter", log[1]);
        Assert.Equal("B exit", log[^2]);
        Assert.Equal("A exit", log[^1]);
        Assert.Contains("C enter", log);
        Assert.Contains("C exit", log);
    }

    [Fact]
    public async Task ShouldSkipTheStreamingCallOfALayerThatOnlyOverridesGetResponse()
    {
        List<string> log = [];
        IChatClient client = new ChatClientBuilder(new ScriptedChatClient())
            .Use(inner => new LeakyTracer(inner, "A", log))
            .Use(inner => new TracingChatClient(inner, "B", log))
            .Build();

        await client.GetResponseAsync("hello");
        Assert.Equal(["A enter", "B enter", "B exit", "A exit"], log);

        log.Clear();
        await foreach (var _ in client.GetStreamingResponseAsync("hello")) { }

        Assert.Equal(["B enter", "B exit"], log);   // A forwarded the stream without seeing it
    }

    // --- streaming ---

    [Fact]
    public async Task ShouldFoldAStreamIntoTextUsageAndFinishReason()
    {
        var chunks = new List<string>();

        var result = await StreamReader.ReadAsync(new ScriptedChatClient().GetStreamingResponseAsync("hi"), chunks.Add);

        Assert.Equal(["Par", "is"], chunks);
        Assert.Equal("Paris", result.Text);
        Assert.Equal(17, result.Usage!.InputTokenCount);
        Assert.Equal(2, result.Usage.OutputTokenCount);
        Assert.Equal(ChatFinishReason.Stop, result.FinishReason);
        Assert.True(result.Completed);
    }

    [Fact]
    public async Task ShouldFlagAStreamThatEndedWithoutAFinishReasonAsPartial()
    {
        var cutOff = new ScriptedChatClient(completeStream: false);

        var result = await StreamReader.ReadAsync(cutOff.GetStreamingResponseAsync("hi"));

        Assert.Equal("Paris", result.Text);   // looks like a normal answer...
        Assert.Null(result.FinishReason);
        Assert.Null(result.Usage);
        Assert.False(result.Completed);        // ...but nothing says it ended
    }

    // --- cache, and where the layers sit around it ---

    [Fact]
    public async Task ShouldLetALayerOutsideTheCacheSeeEveryCall()
    {
        var model = new ScriptedChatClient();
        var tally = new CallTally();
        var client = Pipelines.BuildCached(model, NewCache(), tally, countOutsideCache: true);

        await client.GetResponseAsync("same text");
        await client.GetResponseAsync("same text");

        Assert.Equal(2, tally.Count);
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task ShouldHideCacheHitsFromALayerInsideTheCache()
    {
        var model = new ScriptedChatClient();
        var tally = new CallTally();
        var client = Pipelines.BuildCached(model, NewCache(), tally, countOutsideCache: false);

        await client.GetResponseAsync("same text");
        await client.GetResponseAsync("same text");

        Assert.Equal(1, tally.Count);
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task ShouldKeyTheCacheOnMessagesOptionsAndStreaming()
    {
        var model = new ScriptedChatClient();
        var client = Pipelines.BuildCached(model, NewCache(), new CallTally(), countOutsideCache: false);

        await client.GetResponseAsync("same text", new ChatOptions { Temperature = 0 });
        await client.GetResponseAsync("same text", new ChatOptions { Temperature = 0 });
        Assert.Equal(1, model.Calls);

        await client.GetResponseAsync("same text", new ChatOptions { Temperature = 0.7f });   // another option misses
        await client.GetResponseAsync("Same text", new ChatOptions { Temperature = 0 });      // one capital letter misses
        Assert.Equal(3, model.Calls);

        await foreach (var _ in client.GetStreamingResponseAsync("same text", new ChatOptions { Temperature = 0 })) { }
        await client.GetResponseAsync("same text", new ChatOptions { Temperature = 0 });
        Assert.Equal(4, model.Calls);   // the streaming call missed; the non-streaming entry was still there
    }

    [Fact]
    public async Task ShouldReturnACopyThatStillCarriesTheOriginalUsage()
    {
        var model = new ScriptedChatClient();
        var client = Pipelines.BuildCached(model, NewCache(), new CallTally(), countOutsideCache: false);

        var first = await client.GetResponseAsync("same text");
        var second = await client.GetResponseAsync("same text");

        Assert.Equal(1, model.Calls);
        Assert.NotSame(first, second);                                            // deserialized from the cache
        Assert.Equal(first.Usage!.OutputTokenCount, second.Usage!.OutputTokenCount);   // tokens nobody paid for
    }

    // --- DI, metadata and swapping the provider ---

    [Fact]
    public async Task ShouldRegisterTheCachedPipelineAsASingleton()
    {
        var model = new ScriptedChatClient();
        using var provider = new ServiceCollection().AddModels(model, new ScriptedChatClient("from the fake")).BuildServiceProvider();

        var main = provider.GetRequiredService<IChatClient>();

        Assert.Same(main, provider.GetRequiredService<IChatClient>());
        Assert.NotNull(main.GetService<DistributedCachingChatClient>());
        await main.GetResponseAsync("same text");
        await main.GetResponseAsync("same text");
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task ShouldResolveTheFakeByKey()
    {
        using var provider = new ServiceCollection()
            .AddModels(new ScriptedChatClient(), new ScriptedChatClient("from the fake")).BuildServiceProvider();

        var fake = provider.GetRequiredKeyedService<IChatClient>("fake");

        Assert.Equal("from the fake", (await fake.GetResponseAsync("hello")).Text);
        Assert.Null(fake.GetService<DistributedCachingChatClient>());   // the keyed one has no cache layer
    }

    [Fact]
    public void ShouldReadProviderMetadataThroughThePipeline()
    {
        var piped = new ChatClientBuilder(new ScriptedChatClient()).UseDistributedCache(NewCache()).Build();

        Assert.Equal("fake/fake-model", ProviderInfo.Describe(piped));
        Assert.Equal("unknown", ProviderInfo.Describe(new ScriptedChatClient(describeItself: false)));
    }

    [Fact]
    public async Task ShouldRunTheSameMethodAgainstAnyProviderAndTolerateMissingFields()
    {
        var full = new ScriptedChatClient("Hello!");
        var bare = new ScriptedChatClient("from the fake", describeItself: false, reportUsage: false);

        Assert.Equal("Hello! (fake-model, out tokens=2)", await ProviderInfo.SayHelloAsync(full));
        Assert.Equal("from the fake (unknown, out tokens=unknown)", await ProviderInfo.SayHelloAsync(bare));
        Assert.Equal(["system", "user"], full.Requests[0].Select(m => m.Role.Value));
    }
}
