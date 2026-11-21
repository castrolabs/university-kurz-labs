using Microsoft.Extensions.AI;

namespace ToolCallingLab;

public static class ManualLoop
{
    /// <summary>Runs the tool-calling loop by hand and returns the whole message list.</summary>
    public static async Task<List<ChatMessage>> RunAsync(IChatClient client, string prompt, ChatOptions options)
    {
        List<ChatMessage> messages = [new(ChatRole.User, prompt)];
        while (true)
        {
            ChatResponse response = await client.GetResponseAsync(messages, options);
            messages.AddMessages(response);

            var calls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
            if (calls.Count == 0)
            {
                return messages;
            }

            foreach (var call in calls)
            {
                var function = options.Tools!.OfType<AIFunction>().Single(f => f.Name == call.Name);
                object? result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments));
                messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, result)]));
            }
        }
    }
}

public sealed record ToolLoopSettings(
    int MaxIterations = 40,
    int MaxConsecutiveErrors = 3,
    bool DetailedErrors = false,
    bool Concurrent = false,
    ISet<string>? BlockedTools = null);

public static class ToolPipeline
{
    public static IChatClient Build(IChatClient model, ToolLoopSettings? settings = null)
    {
        settings ??= new ToolLoopSettings();
        return new ChatClientBuilder(model)
            .UseFunctionInvocation(configure: f =>
            {
                f.MaximumIterationsPerRequest = settings.MaxIterations;
                f.MaximumConsecutiveErrorsPerRequest = settings.MaxConsecutiveErrors;
                f.IncludeDetailedErrors = settings.DetailedErrors;
                f.AllowConcurrentInvocation = settings.Concurrent;
                f.FunctionInvoker = async (context, cancellationToken) =>
                    settings.BlockedTools?.Contains(context.Function.Name) == true
                        ? "Not allowed: the current user lacks the admin role."
                        : await context.Function.InvokeAsync(context.Arguments, cancellationToken);
            })
            .Build();
    }
}

public static class ToolResponses
{
    /// <summary>The loop hit its limit: the last message is still a function call nobody answered.</summary>
    public static bool EndedWithUnansweredCall(ChatResponse response) =>
        response.Messages.LastOrDefault()?.Contents.OfType<FunctionCallContent>().Any() == true;
}

public static class ApprovalFlow
{
    /// <summary>Wraps the function so the loop stops for approval, then answers the requests the approver decides.</summary>
    public static async Task<ChatResponse> RunAsync(
        IChatClient client, string prompt, AIFunction function, Func<ToolApprovalRequestContent, bool> approver)
    {
        var options = new ChatOptions { Tools = [new ApprovalRequiredAIFunction(function)] };
        List<ChatMessage> history = [new(ChatRole.User, prompt)];
        while (true)
        {
            ChatResponse response = await client.GetResponseAsync(history, options);
            var request = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().FirstOrDefault();
            if (request is null)
            {
                return response;
            }

            history.AddMessages(response);
            bool approved = approver(request);
            history.Add(new ChatMessage(ChatRole.User, [request.CreateResponse(approved, approved ? null : "User declined")]));
        }
    }
}
