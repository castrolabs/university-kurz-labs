using Microsoft.Extensions.AI;

namespace ToolCallingLab.Tests;

/// <summary>
/// Deterministic IChatClient driven by a script: the function receives the index of the call (0, 1, ...)
/// and returns the response. Records every request and how many tools came with it.
/// Swap it for new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct") to run against Ollama.
/// </summary>
public sealed class ScriptedChatClient(Func<int, ChatResponse> script) : IChatClient
{
    public int Calls { get; private set; }

    public List<List<ChatMessage>> Requests { get; } = [];

    public List<int> ToolsSent { get; } = [];

    /// <summary>The model asks for one tool call (100 tokens in, 10 out).</summary>
    public static ChatResponse Call(string callId, string name, Dictionary<string, object?>? arguments = null) =>
        Reply(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)]));

    /// <summary>The model asks for several tool calls in one turn.</summary>
    public static ChatResponse Calls_(params FunctionCallContent[] calls) =>
        Reply(new ChatMessage(ChatRole.Assistant, [.. calls]));

    /// <summary>The model answers in text (100 tokens in, 10 out).</summary>
    public static ChatResponse Text(string text) =>
        Reply(new ChatMessage(ChatRole.Assistant, text), ChatFinishReason.Stop);

    private static ChatResponse Reply(ChatMessage message, ChatFinishReason? finish = null) =>
        new(message)
        {
            FinishReason = finish,
            Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 10, TotalTokenCount = 110 },
        };

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        ToolsSent.Add(options?.Tools?.Count ?? 0);
        return Task.FromResult(script(Calls++));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The lab only uses GetResponseAsync.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
