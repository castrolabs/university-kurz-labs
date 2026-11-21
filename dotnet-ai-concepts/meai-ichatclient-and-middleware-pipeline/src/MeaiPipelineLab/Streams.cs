using Microsoft.Extensions.AI;

namespace MeaiPipelineLab;

/// <param name="Completed">True only when the stream carried a finish reason.</param>
public sealed record StreamResult(string Text, UsageDetails? Usage, ChatFinishReason? FinishReason, bool Completed);

public static class StreamReader
{
    public static async Task<StreamResult> ReadAsync(
        IAsyncEnumerable<ChatResponseUpdate> updates, Action<string>? onText = null)
    {
        // TODO-05: Read the stream once. Call onText for every update whose Text is not empty (a UI would print
        //          it), keep the updates, and fold them into one ChatResponse (ToChatResponse) when the
        //          stream ends: that moves the UsageContent into Usage. A stream that ended with no
        //          FinishReason is a partial answer: set Completed accordingly.
        await foreach (var update in updates)
        {
        }

        throw new NotImplementedException("TODO-05");
    }
}
