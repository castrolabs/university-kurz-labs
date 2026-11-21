using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace StructuredOutputLab.Tests;

/// <summary>
/// Deterministic IChatClient: answers with the scripted texts in order (the last one repeats) and records
/// every request. Swap it for new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct")
/// to run against Ollama.
/// </summary>
public sealed class ScriptedChatClient(params string[] replies) : IChatClient
{
    public int Calls { get; private set; }

    public List<List<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    private string Next() => replies[Math.Min(Calls++, replies.Length - 1)];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        Options.Add(options);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Next()))
        {
            FinishReason = ChatFinishReason.Stop,
            Usage = new UsageDetails { InputTokenCount = 82, OutputTokenCount = 110, TotalTokenCount = 192 },
        });
    }

    /// <summary>Streams the next reply in chunks of 12 characters: no chunk is valid JSON by itself.</summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        Options.Add(options);
        string text = Next();
        for (int i = 0; i < text.Length; i += 12)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, text.Substring(i, Math.Min(12, text.Length - i)));
        }

        yield return new ChatResponseUpdate { FinishReason = ChatFinishReason.Stop };
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
