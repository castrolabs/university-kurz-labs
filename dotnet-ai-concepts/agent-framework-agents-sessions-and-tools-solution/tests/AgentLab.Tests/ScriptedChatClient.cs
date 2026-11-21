using Microsoft.Extensions.AI;

namespace AgentLab.Tests;

/// <summary>
/// Deterministic IChatClient: answers from a script (call number and received messages in, response out) and
/// records every request. The default script replies "reply 1", "reply 2", ... Swap it for
/// new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct") to run against Ollama.
/// </summary>
public sealed class ScriptedChatClient(Func<int, IReadOnlyList<ChatMessage>, ChatResponse>? script = null) : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    public int Calls => Requests.Count;

    /// <summary>Runs inside the "model call" (argument: the call number), to control which concurrent call finishes first.</summary>
    public Func<int, Task>? WhileThinking { get; set; }

    public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    public static ChatResponse ToolCalls(params (string Name, Dictionary<string, object?> Args)[] calls) =>
        new(new ChatMessage(ChatRole.Assistant,
            calls.Select((c, i) => (AIContent)new FunctionCallContent($"call-{i + 1}", c.Name, c.Args)).ToList()));

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = messages.ToList();
        Requests.Add(request);
        Options.Add(options?.Clone());
        int call = Calls;
        if (WhileThinking is not null)
        {
            await WhileThinking(call);
        }

        return script?.Invoke(call, request) ?? Text($"reply {call}");
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var update in (await GetResponseAsync(messages, options, cancellationToken)).ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
