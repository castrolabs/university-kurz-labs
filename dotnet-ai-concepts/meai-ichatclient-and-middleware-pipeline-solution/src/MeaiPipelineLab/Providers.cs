using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace MeaiPipelineLab;

public static class ModelRegistration
{
    /// <summary>Registers the cached pipeline as the default IChatClient and the fake under the key "fake".</summary>
    public static IServiceCollection AddModels(this IServiceCollection services, IChatClient provider, IChatClient fake)
    {
        services.AddDistributedMemoryCache();
        services.AddChatClient(provider).UseDistributedCache();
        services.AddKeyedChatClient("fake", fake);
        return services;
    }
}

public static class ProviderInfo
{
    /// <summary>"provider/model" from the metadata reachable through the pipeline, or "unknown".</summary>
    public static string Describe(IChatClient client)
    {
        ChatClientMetadata? metadata = client.GetService<ChatClientMetadata>();
        return metadata is null ? "unknown" : $"{metadata.ProviderName}/{metadata.DefaultModelId ?? "?"}";
    }

    /// <summary>One method, any provider. Reports "text (model, out tokens=N)" with "unknown" for what is missing.</summary>
    public static async Task<string> SayHelloAsync(IChatClient client)
    {
        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, "Reply with one word."), new ChatMessage(ChatRole.User, "Say hello.")],
            new ChatOptions { Temperature = 0 });

        string model = response.ModelId ?? "unknown";
        string tokens = response.Usage?.OutputTokenCount?.ToString() ?? "unknown";
        return $"{response.Text} ({model}, out tokens={tokens})";
    }
}
