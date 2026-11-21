using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace ToolCallingLab.Tests;

public sealed class ToolCallingTests
{
    private static ChatOptions WithTools(params AITool[] tools) => new() { Tools = [.. tools] };

    private static IEnumerable<AIContent> Contents(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(m => m.Contents);

    // --- from a method to an AIFunction ---

    [Fact]
    public async Task ShouldNameALambdaAndDescribeIt()
    {
        AIFunction weather = ToolCatalog.CreateWeather();

        Assert.Equal("GetWeather", weather.Name);
        Assert.False(string.IsNullOrWhiteSpace(weather.Description));
        Assert.Equal("Lisbon: 18 C, light rain", (await weather.InvokeAsync(new AIFunctionArguments { ["city"] = "Lisbon" }))?.ToString());
    }

    [Fact]
    public void ShouldRequireEveryParameterWithoutADefaultEvenWhenNullable()
    {
        JsonElement schema = ToolCatalog.CreateForecast().JsonSchema;

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(["location", "date", "notAfter", "unit", "days", "maxResults"], required);
        Assert.DoesNotContain("includeWind", required);   // the only one with a default
        var props = schema.GetProperty("properties");
        Assert.Equal(["Celsius", "Fahrenheit"], props.GetProperty("unit").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.False(props.GetProperty("includeWind").GetProperty("default").GetBoolean());
        Assert.Equal("How many days, 1 to 7", props.GetProperty("days").GetProperty("description").GetString());   // prose: nothing enforces it
    }

    [Fact]
    public void ShouldChangeTheSchemaWhenTheSerializerOptionsChange()
    {
        // Options with no resolver at all are refused (AIFunctionFactory makes them read-only).
        Assert.Throws<InvalidOperationException>(() => ToolCatalog.CreateForecast(new JsonSerializerOptions()));

        // Plain options with a resolver have neither camelCase nor the string enum converter.
        var plainOptions = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        JsonElement plain = ToolCatalog.CreateForecast(plainOptions).JsonSchema;

        var location = plain.GetProperty("properties").GetProperty("location").GetProperty("properties");
        Assert.True(location.TryGetProperty("City", out _));
        Assert.Equal("integer", plain.GetProperty("properties").GetProperty("unit").GetProperty("type").GetString());
    }

    // --- the loop by hand ---

    [Fact]
    public async Task ShouldAnswerACallWithAResultCarryingTheSameCallId()
    {
        var model = new ScriptedChatClient(i => i == 0
            ? ScriptedChatClient.Call("c1", "GetWeather", new() { ["city"] = "Lisbon" })
            : ScriptedChatClient.Text("It is 18 C in Lisbon."));

        var messages = await ManualLoop.RunAsync(model, "Weather in Lisbon?", WithTools(ToolCatalog.CreateWeather()));

        Assert.Equal(2, model.Calls);
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant], messages.Select(m => m.Role));
        Assert.Equal("c1", Assert.Single(Contents(messages).OfType<FunctionCallContent>()).CallId);
        var result = Assert.Single(Contents(messages).OfType<FunctionResultContent>());
        Assert.Equal("c1", result.CallId);
        Assert.Equal("Lisbon: 18 C, light rain", result.Result?.ToString());
    }

    // --- UseFunctionInvocation ---

    [Fact]
    public async Task ShouldRunTheLoopForYouAndSumTheUsageOfBothModelCalls()
    {
        var model = new ScriptedChatClient(i => i == 0
            ? ScriptedChatClient.Call("c1", "GetWeather", new() { ["city"] = "Lisbon" })
            : ScriptedChatClient.Text("It is 18 C in Lisbon."));
        var client = ToolPipeline.Build(model);

        ChatResponse response = await client.GetResponseAsync("Weather in Lisbon?", WithTools(ToolCatalog.CreateWeather()));

        Assert.Equal(2, model.Calls);
        Assert.Equal(3, response.Messages.Count);            // call, result, answer
        Assert.Equal("It is 18 C in Lisbon.", response.Text);
        Assert.Equal(200, response.Usage!.InputTokenCount);
        Assert.Equal(20, response.Usage.OutputTokenCount);
        Assert.False(ToolResponses.EndedWithUnansweredCall(response));   // a real answer is not an unanswered call
    }

    [Fact]
    public async Task ShouldStopAtTheIterationLimitWithoutThrowing()
    {
        var model = new ScriptedChatClient(i => ScriptedChatClient.Call($"c{i}", "GetWeather", new() { ["city"] = "Lisbon" }));
        int toolRuns = 0;
        var weather = AIFunctionFactory.Create((string city) => { toolRuns++; return "18 C"; }, "GetWeather", "Gets the weather.");
        var client = ToolPipeline.Build(model, new ToolLoopSettings(MaxIterations: 3));

        ChatResponse response = await client.GetResponseAsync("Weather?", WithTools(weather));

        Assert.Equal(4, model.Calls);                         // 3 iterations, then one more ask...
        Assert.Equal([1, 1, 1, 0], model.ToolsSent);          // ...without tools
        Assert.Equal(3, toolRuns);
        Assert.True(ToolResponses.EndedWithUnansweredCall(response));
        Assert.Null(response.FinishReason);
    }

    // --- when a tool fails ---

    private static AIFunction FailingTool() => AIFunctionFactory.Create(
        string (string city) => throw new InvalidOperationException($"Database password expired for {city}"),
        "GetWeather", "Gets the weather.");

    private static ScriptedChatClient AlwaysCallsGetWeather() =>
        new(i => ScriptedChatClient.Call($"c{i}", "GetWeather", new() { ["city"] = "Lisbon" }));

    private static string ToolResultSeenByTheModel(ScriptedChatClient model) =>
        Contents(model.Requests[^1]).OfType<FunctionResultContent>().Last().Result!.ToString()!;

    [Fact]
    public async Task ShouldShowTheExceptionToTheModelOnlyWhenDetailedErrorsAreOn()
    {
        static ScriptedChatClient Model() => new(i => i == 0
            ? ScriptedChatClient.Call("c1", "GetWeather", new() { ["city"] = "Lisbon" })
            : ScriptedChatClient.Text("Sorry."));

        var hidden = Model();
        await ToolPipeline.Build(hidden).GetResponseAsync("Weather?", WithTools(FailingTool()));
        Assert.Equal("Error: Function failed.", ToolResultSeenByTheModel(hidden));

        var detailed = Model();
        await ToolPipeline.Build(detailed, new ToolLoopSettings(DetailedErrors: true)).GetResponseAsync("Weather?", WithTools(FailingTool()));
        Assert.Equal("Error: Function failed. Exception: Database password expired for Lisbon", ToolResultSeenByTheModel(detailed));
    }

    [Fact]
    public async Task ShouldRethrowTheToolsOwnExceptionAfterTooManyConsecutiveErrors()
    {
        var model = AlwaysCallsGetWeather();
        var client = ToolPipeline.Build(model, new ToolLoopSettings(MaxConsecutiveErrors: 2));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync("Weather?", WithTools(FailingTool())));
        Assert.Equal(3, model.Calls);

        var strict = AlwaysCallsGetWeather();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ToolPipeline.Build(strict, new ToolLoopSettings(MaxConsecutiveErrors: 0)).GetResponseAsync("Weather?", WithTools(FailingTool())));
        Assert.Equal(1, strict.Calls);
    }

    [Fact]
    public async Task ShouldTellTheModelAboutAFunctionItDoesNotHaveAndKeepGoing()
    {
        var model = new ScriptedChatClient(i => i == 0
            ? ScriptedChatClient.Call("c1", "DeleteEverything")
            : ScriptedChatClient.Text("I cannot do that."));
        var client = ToolPipeline.Build(model, new ToolLoopSettings(DetailedErrors: true));

        ChatResponse response = await client.GetResponseAsync("Do it", WithTools(ToolCatalog.CreateWeather()));

        Assert.Equal("Error: Requested function \"DeleteEverything\" not found.", ToolResultSeenByTheModel(model));
        Assert.Equal("I cannot do that.", response.Text);
    }

    // --- authorization in the invoker, validation in the tool ---

    [Fact]
    public async Task ShouldBlockAToolInTheInvokerAndStillRunTheOthers()
    {
        var bank = new BankTools();
        var model = new ScriptedChatClient(i => i == 0
            ? ScriptedChatClient.Calls_(
                new FunctionCallContent("a", "GetWeather", new Dictionary<string, object?> { ["city"] = "Lisbon" }),
                new FunctionCallContent("b", "DeleteAccount", new Dictionary<string, object?> { ["id"] = "ACC-1" }))
            : ScriptedChatClient.Text("Done."));
        var client = ToolPipeline.Build(model, new ToolLoopSettings(BlockedTools: new HashSet<string> { "DeleteAccount" }));

        await client.GetResponseAsync("Do both", WithTools(ToolCatalog.CreateWeather(), AIFunctionFactory.Create(bank.DeleteAccount)));

        var results = Contents(model.Requests[1]).OfType<FunctionResultContent>().ToDictionary(r => r.CallId, r => r.Result?.ToString());
        Assert.Equal("Lisbon: 18 C, light rain", results["a"]);
        Assert.StartsWith("Not allowed:", results["b"]);
        Assert.Equal(0, bank.Deletions);
    }

    [Fact]
    public void ShouldValidateWhatTheModelSendsBeforeAnySideEffect()
    {
        var bank = new BankTools();

        Assert.Equal("Rejected: 'to' must match ACC-<digits>.", bank.Transfer("'; DROP TABLE x;--", 10));
        Assert.Equal("Rejected: amount must be between 0.01 and 1000 euros.", bank.Transfer("ACC-42", 5000));
        Assert.Equal(0, bank.Transfers);

        Assert.Equal("Transferred 250 to ACC-42.", bank.Transfer("ACC-42", 250));
        Assert.Equal(1, bank.Transfers);
    }

    // --- approval ---

    private static ScriptedChatClient WantsATransfer() => new(i => i == 0
        ? ScriptedChatClient.Call("t1", "Transfer", new() { ["to"] = "ACC-42", ["amount"] = 250 })
        : ScriptedChatClient.Text("Done."));

    [Fact]
    public async Task ShouldStopForApprovalAndRunTheToolOnlyWhenApproved()
    {
        var bank = new BankTools();
        var model = WantsATransfer();
        var client = ToolPipeline.Build(model);
        int asked = 0;

        ChatResponse response = await ApprovalFlow.RunAsync(
            client, "Transfer 250 euros to ACC-42.", AIFunctionFactory.Create(bank.Transfer), _ => { asked++; return true; });

        Assert.Equal(1, asked);
        Assert.Equal(1, bank.Transfers);
        Assert.Equal("Done.", response.Text);
        Assert.Equal(2, model.Calls);   // the approval round trip never reached the model
        Assert.Equal("Transferred 250 to ACC-42.", ToolResultSeenByTheModel(model));
    }

    [Fact]
    public async Task ShouldTellTheModelWhenTheApproverDeclines()
    {
        var bank = new BankTools();
        var model = WantsATransfer();

        await ApprovalFlow.RunAsync(
            ToolPipeline.Build(model), "Transfer 250 euros to ACC-42.", AIFunctionFactory.Create(bank.Transfer), _ => false);

        Assert.Equal(0, bank.Transfers);
        Assert.Contains("rejected by the approver", ToolResultSeenByTheModel(model));
        Assert.Contains("User declined", ToolResultSeenByTheModel(model));
    }

    [Fact]
    public async Task ShouldNotEnforceApprovalWhenTheWrappedFunctionIsInvokedDirectly()
    {
        var bank = new BankTools();
        AIFunction guarded = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(bank.Transfer));

        await guarded.InvokeAsync(new AIFunctionArguments { ["to"] = "ACC-42", ["amount"] = 250m });

        Assert.Equal(1, bank.Transfers);   // approval is cooperative: only the invoker enforces it
    }
}
