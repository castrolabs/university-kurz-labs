using Microsoft.Extensions.AI;

namespace MeaiPipelineLab;

/// <param name="Completed">True only when the stream carried a finish reason.</param>
public sealed record StreamResult(string Text, UsageDetails? Usage, ChatFinishReason? FinishReason, bool Completed);

public static class StreamReader
{
    public static async Task<StreamResult> ReadAsync(
        IAsyncEnumerable<ChatResponseUpdate> updates, Action<string>? onText = null)
    {
        List<ChatResponseUpdate> collected = [];
        await foreach (var update in updates)
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                onText?.Invoke(update.Text);
            }

            collected.Add(update);
        }

        ChatResponse response = collected.ToChatResponse();
        return new StreamResult(response.Text, response.Usage, response.FinishReason, response.FinishReason is not null);
    }
}
