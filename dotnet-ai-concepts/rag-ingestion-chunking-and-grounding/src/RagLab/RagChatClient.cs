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

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        // TODO-09 (optional): Call AugmentAsync, send the augmented messages down the pipeline with
        //          base.GetResponseAsync and put the source ids in response.AdditionalProperties[SourcesKey].
        throw new NotImplementedException("Not implemented yet.");
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // TODO-10 (optional): The same grounding for streaming. A method a DelegatingChatClient does not
        //          override skips the layer, so without this the streaming path would not be grounded.
        await Task.CompletedTask;
        throw new NotImplementedException("Not implemented yet.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
