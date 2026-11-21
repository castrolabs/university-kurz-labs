using Microsoft.Extensions.AI;

namespace MeaiPipelineLab;

/// <summary>An IChatClient keeps no conversation: this class owns the history and resends it.</summary>
public sealed class Conversation(IChatClient client, string? systemPrompt = null)
{
    private readonly List<ChatMessage> _history = systemPrompt is null ? [] : [new(ChatRole.System, systemPrompt)];

    public IReadOnlyList<ChatMessage> History => _history;

    public async Task<string> AskAsync(string question, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _history.Add(new ChatMessage(ChatRole.User, question));
        ChatResponse response = await client.GetResponseAsync(_history, options, cancellationToken);
        _history.AddMessages(response);
        return response.Text;
    }
}
