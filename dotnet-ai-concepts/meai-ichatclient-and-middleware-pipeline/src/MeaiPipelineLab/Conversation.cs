using Microsoft.Extensions.AI;

namespace MeaiPipelineLab;

/// <summary>An IChatClient keeps no conversation: this class owns the history and resends it.</summary>
public sealed class Conversation(IChatClient client, string? systemPrompt = null)
{
    private readonly List<ChatMessage> _history = systemPrompt is null ? [] : [new(ChatRole.System, systemPrompt)];

    public IReadOnlyList<ChatMessage> History => _history;

    public async Task<string> AskAsync(string question, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        // TODO-00: Add the question as a user message, send the WHOLE history to the client, append every
        //          message of the response to the history (AddMessages) and return the response text.
        //          The client forgets everything between calls, so the history is yours to keep.
        await Task.CompletedTask;
        throw new NotImplementedException("TODO-00");
    }
}
