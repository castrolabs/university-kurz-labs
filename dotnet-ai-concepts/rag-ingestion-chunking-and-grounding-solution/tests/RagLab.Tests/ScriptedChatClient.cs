using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace RagLab.Tests;

/// <summary>
/// Deterministic IChatClient: answers with whatever the script returns for the messages it received and
/// records every request. Swap it for new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct")
/// to run against Ollama.
/// </summary>
public sealed class ScriptedChatClient(Func<IReadOnlyList<ChatMessage>, string> script) : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    public int Calls => Requests.Count;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = messages.ToList();
        Requests.Add(request);
        Options.Add(options);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, script(request))));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = messages.ToList();
        Requests.Add(request);
        Options.Add(options);
        foreach (var word in script(request).Split(' '))
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ");
        }

        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
