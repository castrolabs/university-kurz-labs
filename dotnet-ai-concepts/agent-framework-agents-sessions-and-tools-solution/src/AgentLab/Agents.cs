using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentLab;

public static class Agents
{
    public static AIAgent CreateHelper(IChatClient client, string instructions, string name) =>
        client.AsAIAgent(instructions: instructions, name: name);

    public static AIAgent WithTools(IChatClient client, WeatherService weather) =>
        client.AsAIAgent(
            instructions: "Answer in one sentence.",
            tools: [AIFunctionFactory.Create(weather.GetWeather)]);

    public static AIAgent WithApproval(IChatClient client, BankService bank) =>
        client.AsAIAgent(
            instructions: "Answer in one sentence.",
            tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(bank.Transfer))]);

    public static AIAgent WithPolicy(AIAgent inner, List<string> log) =>
        inner.AsBuilder()
            .Use(async (messages, session, options, next, cancellationToken) =>
            {
                log.Add("run enter");
                await next(messages, session, options, cancellationToken);
                log.Add("run exit");
            })
            .Use(async (agent, context, next, cancellationToken) =>
            {
                log.Add($"function {context.Function.Name}");
                return context.Function.Name == "DeleteAccount"
                    ? "Blocked by policy: DeleteAccount needs a human."
                    : await next(context, cancellationToken);
            })
            .Build();

    /// <summary>Provided: the memory line is injected every turn and stored in the history every turn.</summary>
    public static AIAgent WithCustomerContext(IChatClient client) =>
        client.AsAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Instructions = "Be brief." },
            AIContextProviders = [new CustomerContext()],
        });

    public static AIAgent WithCustomerContextFiltered(IChatClient client) =>
        client.AsAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Instructions = "Be brief." },
            AIContextProviders = [new CustomerContext()],
            ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
            {
                StorageInputRequestMessageFilter = messages => messages.Where(
                    m => m.GetAgentRequestMessageSourceType() == AgentRequestMessageSourceType.External),
            }),
        });

    public static AIAgent WithSpecialist(IChatClient parent, AIAgent specialist) =>
        parent.AsAIAgent(
            instructions: "Call the specialist for conversions.",
            tools: [specialist.AsAIFunction()]);

    public static AIAgent WithOwnPipeline(IChatClient model, WeatherService weather, bool functionInvocation)
    {
        var builder = new ChatClientBuilder(model);
        if (functionInvocation)
        {
            builder.UseFunctionInvocation();
        }

        return builder.Build().AsAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Tools = [AIFunctionFactory.Create(weather.GetWeather)] },
            UseProvidedChatClientAsIs = true,
        });
    }
}

public sealed class CustomerContext : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new AIContext
        {
            Instructions = "The customer is on the Premium plan.",
            Messages = [new ChatMessage(ChatRole.User, "Memory: the customer lives in Porto.")],
        });
}
