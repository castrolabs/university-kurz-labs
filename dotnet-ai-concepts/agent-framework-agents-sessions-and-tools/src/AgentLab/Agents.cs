using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentLab;

public static class Agents
{
    public static AIAgent CreateHelper(IChatClient client, string instructions, string name)
    {
        // TODO-00: Turn the IChatClient into an agent with the AsAIAgent extension (Microsoft.Agents.AI),
        //          passing the instructions and the name. Then read the first test: where do the
        //          instructions travel, and are they part of the conversation history?
        throw new NotImplementedException("Not implemented yet.");
    }

    public static AIAgent WithTools(IChatClient client, WeatherService weather)
    {
        // TODO-04: An agent with the instructions "Answer in one sentence." and ONE tool: weather.GetWeather
        //          as an AIFunction (AIFunctionFactory.Create). You write no loop: the agent adds its own
        //          function-invocation layer and runs the tool when the model asks for it.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static AIAgent WithApproval(IChatClient client, BankService bank)
    {
        // TODO-05: Same shape as WithTools, with bank.Transfer as the tool, but a human must approve every call:
        //          wrap the AIFunction in ApprovalRequiredAIFunction. The run then stops and returns an
        //          approval request instead of running the tool.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static AIAgent WithPolicy(AIAgent inner, List<string> log)
    {
        // TODO-07: Wrap the agent with inner.AsBuilder() and two middleware layers, then Build():
        //          (1) run middleware (the overload with messages, session, options, next, token): add
        //              "run enter" to the log, await next(...), add "run exit";
        //          (2) function-calling middleware (the overload with agent, context, next, token): add
        //              "function {context.Function.Name}" to the log, and for DeleteAccount return the string
        //              "Blocked by policy: DeleteAccount needs a human." WITHOUT calling next; any other
        //              function goes through next(context, token).
        throw new NotImplementedException("Not implemented yet.");
    }

    /// <summary>Given: the memory line is injected every turn and stored in the history every turn.</summary>
    public static AIAgent WithCustomerContext(IChatClient client) =>
        client.AsAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Instructions = "Be brief." },
            AIContextProviders = [new CustomerContext()],
        });

    public static AIAgent WithCustomerContextFiltered(IChatClient client)
    {
        // TODO-08: Like WithCustomerContext, but keep the injected memory out of the stored history. Give the
        //          options an InMemoryChatHistoryProvider whose InMemoryChatHistoryProviderOptions set
        //          StorageInputRequestMessageFilter to keep only messages whose GetAgentRequestMessageSourceType()
        //          is AgentRequestMessageSourceType.External (what the caller sent). The model still sees the memory.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static AIAgent WithSpecialist(IChatClient parent, AIAgent specialist)
    {
        // TODO-09 (optional): An agent over the parent client with instructions "Call the specialist for
        //          conversions." and the specialist as its only tool (AsAIFunction). Name and description of
        //          the specialist are the routing prompt the parent's model sees. Count the model calls of
        //          each client: is the specialist's usage inside the parent's?
        throw new NotImplementedException("Not implemented yet.");
    }

    public static AIAgent WithOwnPipeline(IChatClient model, WeatherService weather, bool functionInvocation)
    {
        // TODO-10 (optional): Build your own pipeline with ChatClientBuilder (add UseFunctionInvocation only when
        //          functionInvocation is true) and make the agent use it AS IS: ChatClientAgentOptions with
        //          UseProvidedChatClientAsIs = true and weather.GetWeather in ChatOptions.Tools. What does the
        //          agent return when nothing runs the tool loop?
        throw new NotImplementedException("Not implemented yet.");
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
