using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace RagLab;

/// <summary>Keeps the app IChatClient-shaped: callers send the question, this layer adds the grounding.</summary>
public sealed class RagChatClient(IChatClient inner, RagStore store, int k = 3) : DelegatingChatClient(inner)
{
    public const string SourcesKey = "rag.sources";

    private async Task<(List<ChatMessage> Messages, string[] Ids)> AugmentAsync(IEnumerable<ChatMessage> messages)
    {
        var list = messages.ToList();
        var question = list.Last(m => m.Role == ChatRole.User).Text;
        var sources = (await Retriever.SearchAsync(store, question, k)).Select(r => r.Chunk).ToList();
        list.Insert(0, new ChatMessage(ChatRole.System, $"{PromptBuilder.Rules}\n\n{PromptBuilder.FormatSources(sources)}"));
        return (list, sources.Select(s => s.Id).ToArray());
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var (augmented, ids) = await AugmentAsync(messages);
        var response = await base.GetResponseAsync(augmented, options, cancellationToken);
        (response.AdditionalProperties ??= [])[SourcesKey] = ids;
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (augmented, _) = await AugmentAsync(messages);
        await foreach (var update in base.GetStreamingResponseAsync(augmented, options, cancellationToken))
        {
            yield return update;
        }
    }
}
