using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace StructuredOutputLab;

public static class TicketExtractor
{
    public static async Task<Ticket> ExtractAsync(IChatClient client, string email, CancellationToken cancellationToken = default)
    {
        ChatResponse<Ticket> response = await client.GetResponseAsync<Ticket>(
            "Extract the ticket from this email:\n" + email,
            new ChatOptions { Temperature = 0, Seed = 42 },
            cancellationToken: cancellationToken);
        return response.Result;
    }
}

public static class SchemaInspector
{
    /// <summary>The names under "required" in the schema MEAI generates for T.</summary>
    public static IReadOnlyList<string> Required<T>()
    {
        JsonElement schema = ChatResponseFormat.ForJsonSchema<T>().Schema!.Value;
        return schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToList();
    }
}

public static class SafeParse
{
    /// <summary>Asks for a T and turns every parsing failure into an outcome instead of an exception.</summary>
    public static async Task<ParseOutcome<T>> TryExtractAsync<T>(IChatClient client, string prompt = "Extract the data.")
    {
        ChatResponse<T> response = await client.GetResponseAsync<T>(prompt);
        try
        {
            return new ParseOutcome<T>(response.Result, null);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return new ParseOutcome<T>(default, $"{e.GetType().Name}: {e.Message}");
        }
    }
}

public static class Primitives
{
    public static async Task<string[]> PrimaryColorsAsync(IChatClient client) =>
        (await client.GetResponseAsync<string[]>("Name the three primary colors.")).Result;

    public static async Task<int> CountWheelsAsync(IChatClient client) =>
        (await client.GetResponseAsync<int>("How many wheels does a car have?")).Result;
}

public static class Classifier
{
    public static async Task<Category> ClassifyAsync(IChatClient client, string message) =>
        (await client.GetResponseAsync<Category>("Classify this support message:\n" + message)).Result;
}

public static class OrderValidator
{
    /// <summary>Deserialization is not validation: checks the [Range] and friends after parsing.</summary>
    public static IReadOnlyList<string> Validate(object value)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), errors, validateAllProperties: true);
        return errors.Select(e => e.ErrorMessage ?? "invalid").ToList();
    }
}

public static class RetryingExtractor
{
    /// <summary>Parse, validate, and on failure feed the error back. At most maxAttempts model calls.</summary>
    public static async Task<RetryOutcome<T>> ExtractAsync<T>(IChatClient client, string prompt, int maxAttempts)
        where T : class
    {
        List<ChatMessage> messages = [new(ChatRole.User, prompt)];
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ChatResponse<T> response = await client.GetResponseAsync<T>(messages);
            string error;
            try
            {
                T value = response.Result;
                var problems = OrderValidator.Validate(value);
                if (problems.Count == 0)
                {
                    return new RetryOutcome<T>(value, attempt);
                }

                error = string.Join("; ", problems);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException)
            {
                error = e.Message;
            }

            messages.AddMessages(response);
            messages.Add(new ChatMessage(ChatRole.User, $"That answer was rejected: {error} Reply again with corrected JSON."));
        }

        return new RetryOutcome<T>(null, maxAttempts);
    }
}

public static class JsonText
{
    /// <summary>Reads reply text with the options the schema was built from.</summary>
    public static T? Parse<T>(string text) => JsonSerializer.Deserialize<T>(text, AIJsonUtilities.DefaultOptions);
}

public static class StreamingExtractor
{
    /// <summary>There is no streaming GetResponseAsync&lt;T&gt;: stream, then parse the folded response.</summary>
    public static async Task<T> ExtractAsync<T>(IChatClient client, string prompt)
    {
        List<ChatResponseUpdate> updates = [];
        await foreach (var update in client.GetStreamingResponseAsync(
            prompt, new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema<T>() }))
        {
            updates.Add(update);
        }

        return new ChatResponse<T>(updates.ToChatResponse(), AIJsonUtilities.DefaultOptions).Result;
    }
}

public static class OptionalExtras
{
    /// <summary>Schema in the prompt instead of in the request format (for a model without native schema support).</summary>
    public static async Task<Ticket> ExtractWithPromptSchemaAsync(IChatClient client, string email) =>
        (await client.GetResponseAsync<Ticket>(
            "Extract the ticket from this email:\n" + email, useJsonSchemaResponseFormat: false)).Result;

    /// <summary>Without "?" a model may write the string "null" for a missing fact, and that passes "required".</summary>
    public static string? NullIfLiteralNull(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : value;
}
