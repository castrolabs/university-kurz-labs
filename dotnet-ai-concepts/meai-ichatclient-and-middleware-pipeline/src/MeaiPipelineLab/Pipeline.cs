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
        // TODO-01: Add "{name} enter" to the log, call the client below (base), and add "{name} exit"
        //          afterwards, even when the call below throws.
        return await base.GetResponseAsync(messages, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // TODO-02: Same enter/exit pair around the whole stream. A DelegatingChatClient forwards every
        //          method you do not override, so a layer that overrides only GetResponseAsync never
        //          sees a streaming call. "exit" belongs after the last update has been yielded.
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
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
    public static IChatClient BuildTraced(IChatClient model, List<string> log)
    {
        // TODO-03: With ChatClientBuilder, stack three layers: a TracingChatClient named "A", another named
        //          "B", and a third layer written as ONE delegate passed to Use (the overload shared by
        //          both calls) that logs "C enter", awaits next(...) and logs "C exit".
        //          The first Use is the outermost layer: A sees the request first and the response last.
        return model;
    }

    public static IChatClient BuildCached(IChatClient model, IDistributedCache cache, CallTally tally, bool countOutsideCache)
    {
        // TODO-04: Build a pipeline with UseDistributedCache(cache) and a CountingChatClient(inner, tally).
        //          When countOutsideCache is true the counter must see every call (cache hits included);
        //          when false it must only see the calls the cache lets through.
        return model;
    }

    public static IChatClient BuildLogged(IChatClient model, ILoggerFactory loggerFactory)
    {
        // TODO-09 (optional): Wrap the model with UseLogging(loggerFactory). At which LogLevel does it write
        //                     "GetResponseAsync invoked."? And where does the full prompt appear?
        return model;
    }
}
