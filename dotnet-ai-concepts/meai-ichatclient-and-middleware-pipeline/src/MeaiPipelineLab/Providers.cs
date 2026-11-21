using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace MeaiPipelineLab;

public static class ModelRegistration
{
    /// <summary>Registers the cached pipeline as the default IChatClient and the fake under the key "fake".</summary>
    public static IServiceCollection AddModels(this IServiceCollection services, IChatClient provider, IChatClient fake)
    {
        // TODO-06: Register an in-memory IDistributedCache, then AddChatClient(provider) followed by
        //          UseDistributedCache() (it takes the cache from DI), and AddKeyedChatClient("fake", fake).
        //          The keyed client must NOT get the cache layer.
        return services;
    }
}

public static class ProviderInfo
{
    /// <summary>"provider/model" from the metadata reachable through the pipeline, or "unknown".</summary>
    public static string Describe(IChatClient client)
    {
        // TODO-07: Ask the client for its ChatClientMetadata (GetService reaches through the layers).
        //          Return "{ProviderName}/{DefaultModelId}" ("?" when the model is null), or "unknown"
        //          when the client offers no metadata.
        throw new NotImplementedException("TODO-07");
    }

    /// <summary>One method, any provider. Reports "text (model, out tokens=N)" with "unknown" for what is missing.</summary>
    public static async Task<string> SayHelloAsync(IChatClient client)
    {
        // TODO-08: Send a system message "Reply with one word." and a user message "Say hello." with
        //          Temperature = 0, then return "{Text} ({ModelId}, out tokens={OutputTokenCount})".
        //          A provider may report no model id and no usage: print "unknown" instead of failing.
        await Task.CompletedTask;
        throw new NotImplementedException("TODO-08");
    }
}
