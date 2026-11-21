using Microsoft.Extensions.AI;

namespace StockMcp.Tests;

/// <summary>
/// A model that has "read the customer note" and now always asks for the same call:
/// reserve 999 units of SKU-1. Once a tool result is in the conversation it answers with plain text.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var sawToolResult = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any();
        var reply = sawToolResult
            ? new ChatMessage(ChatRole.Assistant, "Done.")
            : new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("call-1", "reserve",
                    new Dictionary<string, object?> { ["sku"] = "SKU-1", ["quantity"] = 999 })]);
        return Task.FromResult(new ChatResponse(reply));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
