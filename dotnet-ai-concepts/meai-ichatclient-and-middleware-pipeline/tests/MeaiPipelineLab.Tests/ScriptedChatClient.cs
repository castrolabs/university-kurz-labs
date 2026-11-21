using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace MeaiPipelineLab.Tests;

/// <summary>
/// Deterministic IChatClient: fixed answer, optional usage and metadata, records every request.
/// Swap it for new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct") to run against Ollama.
/// </summary>
/// <param name="describeItself">False gives a bare client: no model id and no ChatClientMetadata.</param>
/// <param name="reportUsage">False leaves Usage null, like a provider that reports nothing.</param>
/// <param name="completeStream">False ends the stream early, with no finish reason and no usage (a cut-off answer).</param>
public sealed class ScriptedChatClient(
    string answer = "Paris", bool describeItself = true, bool reportUsage = true, bool completeStream = true) : IChatClient
{
    public int Calls { get; private set; }

    /// <summary>A snapshot of the messages of every call (the caller's list may change afterwards).</summary>
    public List<List<ChatMessage>> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        Requests.Add(messages.ToList());
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer))
        {
            ModelId = describeItself ? "fake-model" : null,
            FinishReason = ChatFinishReason.Stop,
            Usage = reportUsage ? new UsageDetails { InputTokenCount = 17, OutputTokenCount = 2, TotalTokenCount = 19 } : null,
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Calls++;
        Requests.Add(messages.ToList());
        int cut = Math.Min(3, answer.Length);
        yield return new ChatResponseUpdate(ChatRole.Assistant, answer[..cut]);
        yield return new ChatResponseUpdate(ChatRole.Assistant, answer[cut..]);
        if (completeStream)
        {
            yield return new ChatResponseUpdate
            {
                FinishReason = ChatFinishReason.Stop,
                Contents = reportUsage ? [new UsageContent(new UsageDetails { InputTokenCount = 17, OutputTokenCount = 2, TotalTokenCount = 19 })] : [],
            };
        }

        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        describeItself && serviceType == typeof(ChatClientMetadata) && serviceKey is null
            ? new ChatClientMetadata("fake", new Uri("http://fake.test"), "fake-model")
            : null;

    public void Dispose()
    {
    }
}
