using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentLab.Tests;

public class AgentTests
{
    private static string[] Roles(IEnumerable<ChatMessage> messages) => messages.Select(m => m.Role.Value).ToArray();

    private static List<ChatMessage> History(AgentSession session)
    {
        Assert.True(session.TryGetInMemoryChatHistory(out var messages));
        return messages!;
    }

    private static ChatResponse WeatherCallThen(string answer, int call) => call == 1
        ? ScriptedChatClient.ToolCalls(("GetWeather", new() { ["city"] = "Lisbon" }))
        : ScriptedChatClient.Text(answer);

    [Fact]
    public async Task Instructions_travel_in_the_chat_options_and_never_in_the_history()
    {
        var fake = new ScriptedChatClient();
        var agent = Agents.CreateHelper(fake, "Be brief.", "Helper");

        var response = await agent.RunAsync("hi");

        Assert.Equal("Helper", agent.Name);
        Assert.Equal("reply 1", response.Text);
        Assert.Equal("Be brief.", fake.Options[0]!.Instructions);
        Assert.Equal(["user"], Roles(fake.Requests[0]));
    }

    [Fact]
    public async Task A_session_makes_the_agent_resend_the_whole_history_every_turn()
    {
        var fake = new ScriptedChatClient();
        var agent = Agents.CreateHelper(fake, "Be brief.", "Helper");

        var session = await Conversation.AskAllAsync(agent, ["My name is Ana.", "What is my name?"], useSession: true);

        Assert.Equal(["user"], Roles(fake.Requests[0]));
        Assert.Equal(["user", "assistant", "user"], Roles(fake.Requests[1]));
        Assert.Equal(["My name is Ana.", "reply 1", "What is my name?"], fake.Requests[1].Select(m => m.Text));
        Assert.Equal(4, History(session!).Count);
    }

    [Fact]
    public async Task Without_a_session_every_call_stands_alone()
    {
        var fake = new ScriptedChatClient();
        var agent = Agents.CreateHelper(fake, "Be brief.", "Helper");

        var session = await Conversation.AskAllAsync(agent, ["My name is Ana.", "What is my name?"], useSession: false);

        Assert.Null(session);
        Assert.Equal(["user"], Roles(fake.Requests[1]));
    }

    [Fact]
    public async Task A_serialized_session_continues_with_a_new_agent_and_carries_messages_but_not_instructions()
    {
        var first = new ScriptedChatClient();
        var agent = Agents.CreateHelper(first, "Be brief.", "Helper");
        var session = await Conversation.AskAllAsync(agent, ["My name is Ana and I live in Porto."], useSession: true);

        string json = await Conversation.SerializeAsync(agent, session!);

        Assert.Contains("My name is Ana and I live in Porto.", json);
        Assert.DoesNotContain("Be brief.", json);

        var second = new ScriptedChatClient();
        var newAgent = Agents.CreateHelper(second, "Be brief.", "Helper");
        var restored = await Conversation.RestoreAsync(newAgent, json);
        await newAgent.RunAsync("Where do I live?", restored);

        Assert.Equal(["user", "assistant", "user"], Roles(second.Requests[0]));
        Assert.Equal("Be brief.", second.Options[0]!.Instructions);
    }

    [Fact]
    public async Task A_per_session_lock_keeps_concurrent_runs_in_order_even_when_the_second_would_finish_first()
    {
        // The first concurrent call (number 2) is held until the test releases it; the second (number 3) is instant.
        var releaseFirst = new TaskCompletionSource();
        var fake = new ScriptedChatClient { WhileThinking = call => call == 2 ? releaseFirst.Task : Task.CompletedTask };
        var agent = Agents.CreateHelper(fake, "Be brief.", "Helper");
        var session = (await Conversation.AskAllAsync(agent, ["first"], useSession: true))!;
        var serialized = new SerializedAgent(agent);

        var a = serialized.RunAsync(session, "A");
        var b = serialized.RunAsync(session, "B");
        for (int i = 0; i < 100; i++)
        {
            await Task.Yield();
        }

        releaseFirst.SetResult();
        await Task.WhenAll(a, b);

        Assert.Equal(["first", "reply 1", "A", "reply 2", "B", "reply 3"], History(session).Select(m => m.Text));
    }

    [Fact]
    public async Task The_agent_runs_the_tool_loop_itself()
    {
        var fake = new ScriptedChatClient((call, _) => WeatherCallThen("Lisbon has light rain.", call));
        var weather = new WeatherService();
        var agent = Agents.WithTools(fake, weather);

        var response = await agent.RunAsync("Weather in Lisbon?");

        Assert.Equal(2, fake.Calls);
        Assert.Equal(1, weather.Runs);
        Assert.Equal("Lisbon has light rain.", response.Text);
        var result = fake.Requests[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        Assert.Equal("Lisbon: 18 C, light rain", result.Result?.ToString());
    }

    private static ChatResponse TransferCallThen(string answer, int call) => call == 1
        ? ScriptedChatClient.ToolCalls(("Transfer", new() { ["to"] = "ACC-42", ["amount"] = 250 }))
        : ScriptedChatClient.Text(answer);

    [Fact]
    public async Task An_approved_call_runs_the_tool_on_a_second_run_of_the_same_session()
    {
        var fake = new ScriptedChatClient((call, _) => TransferCallThen("Done.", call));
        var bank = new BankService();
        var agent = Agents.WithApproval(fake, bank);
        var session = await agent.CreateSessionAsync();

        var response = await Approvals.RunWithApprovalAsync(agent, session, "Send 250 to ACC-42", approve: true);

        Assert.Equal([("ACC-42", 250)], bank.Transfers);
        Assert.Equal("Done.", response.Text);
        Assert.Equal(2, fake.Calls);
    }

    [Fact]
    public async Task A_refused_call_never_runs_the_tool()
    {
        var fake = new ScriptedChatClient((call, _) => TransferCallThen("Cancelled.", call));
        var bank = new BankService();
        var agent = Agents.WithApproval(fake, bank);
        var session = await agent.CreateSessionAsync();

        var first = await agent.RunAsync("Send 250 to ACC-42", session);
        Assert.Contains(first.Messages.SelectMany(m => m.Contents), c => c is ToolApprovalRequestContent);
        Assert.Empty(bank.Transfers);

        await Approvals.RunWithApprovalAsync(agent, await agent.CreateSessionAsync(), "Send 250 to ACC-42", approve: false);

        Assert.Empty(bank.Transfers);
    }

    [Fact]
    public async Task Function_middleware_can_block_one_tool_while_run_middleware_wraps_the_whole_run()
    {
        var fake = new ScriptedChatClient((call, _) => call == 1
            ? ScriptedChatClient.ToolCalls(("GetWeather", new() { ["city"] = "Lisbon" }), ("DeleteAccount", new() { ["id"] = "acc-1" }))
            : ScriptedChatClient.Text("Done."));
        var weather = new WeatherService();
        var accounts = new AccountService();
        AIAgent inner = fake.AsAIAgent(
            instructions: "Be brief.",
            tools: [AIFunctionFactory.Create(weather.GetWeather), AIFunctionFactory.Create(accounts.DeleteAccount)]);
        var log = new List<string>();

        await Agents.WithPolicy(inner, log).RunAsync("Check the weather and delete acc-1");

        Assert.Equal(["run enter", "function GetWeather", "function DeleteAccount", "run exit"], log);
        Assert.Equal(1, weather.Runs);
        Assert.Equal(0, accounts.DeleteRuns);
        var results = fake.Requests[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.Result?.ToString());
        Assert.Contains("Blocked by policy: DeleteAccount needs a human.", results);
    }

    [Fact]
    public async Task Injected_context_is_stored_in_the_history_so_it_piles_up_every_turn()
    {
        var fake = new ScriptedChatClient();
        var agent = Agents.WithCustomerContext(fake);
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync("Which plan am I on?", session);
        await agent.RunAsync("And my city?", session);

        Assert.Contains("Be brief.", fake.Options[0]!.Instructions);
        Assert.Contains("The customer is on the Premium plan.", fake.Options[0]!.Instructions);
        Assert.Equal(["Which plan am I on?", "Memory: the customer lives in Porto."], fake.Requests[0].Select(m => m.Text));
        Assert.Equal(6, History(session).Count);
    }

    [Fact]
    public async Task A_storage_filter_keeps_the_injected_memory_out_of_the_history()
    {
        var fake = new ScriptedChatClient();
        var agent = Agents.WithCustomerContextFiltered(fake);
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync("Which plan am I on?", session);
        await agent.RunAsync("And my city?", session);

        Assert.Equal(4, fake.Requests[1].Count);
        Assert.Equal("Memory: the customer lives in Porto.", fake.Requests[1].Last().Text);
        Assert.Equal(4, History(session).Count);
        Assert.DoesNotContain(History(session), m => m.Text.StartsWith("Memory:"));
    }

    // Optional tasks: these two tests pass until you start the optional TODO, then they check your work.

    [Fact]
    public async Task Optional_task_an_agent_becomes_a_tool_and_its_model_calls_are_counted_apart()
    {
        var specialistModel = new ScriptedChatClient((_, _) => ScriptedChatClient.Text("220"));
        var parentModel = new ScriptedChatClient((call, _) => call == 1
            ? ScriptedChatClient.ToolCalls(("CurrencyExpert", new() { ["query"] = "200 euros to us dollars" }))
            : ScriptedChatClient.Text("200 euros is 220 US dollars."));
        AIAgent specialist = specialistModel.AsAIAgent(
            name: "CurrencyExpert", description: "Converts amounts between EUR and USD.", instructions: "Use 1 EUR = 1.10 USD.");

        AgentResponse response;
        try
        {
            response = await Agents.WithSpecialist(parentModel, specialist).RunAsync("How much is 200 euros in dollars?");
        }
        catch (NotImplementedException)
        {
            return;
        }

        Assert.Equal("200 euros is 220 US dollars.", response.Text);
        Assert.Equal(2, parentModel.Calls);
        Assert.Equal(1, specialistModel.Calls);
        Assert.Contains("200 euros to us dollars", specialistModel.Requests[0].Last().Text);
    }

    [Fact]
    public async Task Optional_task_a_client_used_as_is_needs_function_invocation_or_the_tool_never_runs()
    {
        var withoutLoop = new ScriptedChatClient((call, _) => WeatherCallThen("Rain.", call));
        var withLoop = new ScriptedChatClient((call, _) => WeatherCallThen("Rain.", call));
        var weatherA = new WeatherService();
        var weatherB = new WeatherService();

        AgentResponse first;
        AgentResponse second;
        try
        {
            first = await Agents.WithOwnPipeline(withoutLoop, weatherA, functionInvocation: false).RunAsync("Weather?");
            second = await Agents.WithOwnPipeline(withLoop, weatherB, functionInvocation: true).RunAsync("Weather?");
        }
        catch (NotImplementedException)
        {
            return;
        }

        Assert.Equal(1, withoutLoop.Calls);
        Assert.Equal(0, weatherA.Runs);
        Assert.Contains(first.Messages.SelectMany(m => m.Contents), c => c is FunctionCallContent);
        Assert.Equal(2, withLoop.Calls);
        Assert.Equal(1, weatherB.Runs);
        Assert.Equal("Rain.", second.Text);
    }
}
