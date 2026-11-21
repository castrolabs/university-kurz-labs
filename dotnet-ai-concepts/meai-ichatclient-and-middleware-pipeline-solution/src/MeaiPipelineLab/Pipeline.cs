using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace MeaiPipelineLab;

/// <summary>Writes "{name} enter" before the call below it and "{name} exit" after it, for both calls.</summary>
public sealed class TracingChatClient(IChatClient inner, string name, List<string> log) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        log.Add($"{name} enter");
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        finally
        {
            log.Add($"{name} exit");
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        log.Add($"{name} enter");
        try
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            log.Add($"{name} exit");
        }
    }
}

/// <summary>Counts the calls that reach it. Already written: it is a probe for the cache tests.</summary>
public sealed class CallTally
{
    public int Count { get; set; }
}

public sealed class CountingChatClient(IChatClient inner, CallTally tally) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        tally.Count++;
        return base.GetResponseAsync(messages, options, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        tally.Count++;
        return base.GetStreamingResponseAsync(messages, options, cancellationToken);
    }
}

public static class Pipelines
{
    public static IChatClient BuildTraced(IChatClient model, List<string> log) =>
        new ChatClientBuilder(model)
            .Use(inner => new TracingChatClient(inner, "A", log))
            .Use(inner => new TracingChatClient(inner, "B", log))
            .Use(async (messages, options, next, cancellationToken) =>
            {
                log.Add("C enter");
                await next(messages, options, cancellationToken);
                log.Add("C exit");
            })
            .Build();

    public static IChatClient BuildCached(IChatClient model, IDistributedCache cache, CallTally tally, bool countOutsideCache)
    {
        var builder = new ChatClientBuilder(model);
        if (countOutsideCache)
        {
            builder.Use(inner => new CountingChatClient(inner, tally)).UseDistributedCache(cache);
        }
        else
        {
            builder.UseDistributedCache(cache).Use(inner => new CountingChatClient(inner, tally));
        }

        return builder.Build();
    }

    public static IChatClient BuildLogged(IChatClient model, ILoggerFactory loggerFactory) =>
        new ChatClientBuilder(model).UseLogging(loggerFactory).Build();
}
