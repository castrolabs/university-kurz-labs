using Microsoft.Extensions.AI;

namespace ToolCallingLab;

public static class ManualLoop
{
    /// <summary>Runs the tool-calling loop by hand and returns the whole message list.</summary>
    public static async Task<List<ChatMessage>> RunAsync(IChatClient client, string prompt, ChatOptions options)
    {
        // TODO-02: Start with one user message. Loop: send the messages, append every message of the response
        //          (AddMessages), collect the FunctionCallContent items of the response and stop when there are
        //          none. For each call find the AIFunction in options.Tools by name, InvokeAsync it with
        //          new AIFunctionArguments(call.Arguments) and append a ChatRole.Tool message holding a
        //          FunctionResultContent with the SAME CallId. Return the whole list.
        await Task.CompletedTask;
        throw new NotImplementedException("TODO-02");
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
                // TODO-03: Apply settings.MaxIterations (MaximumIterationsPerRequest) and
                //          settings.MaxConsecutiveErrors (MaximumConsecutiveErrorsPerRequest).

                // TODO-04: Apply settings.DetailedErrors (IncludeDetailedErrors). What does the model see
                //          with it off? And with it on? Who else sees that text?

                // TODO-05: Set f.FunctionInvoker: when settings.BlockedTools contains context.Function.Name,
                //          do not run the function and return "Not allowed: the current user lacks the admin
                //          role."; otherwise run context.Function.InvokeAsync(context.Arguments, cancellationToken).

                // TODO-09 (optional): Apply settings.Concurrent (AllowConcurrentInvocation). Write a tool that
                //                     records how many calls run at the same time and compare both settings.
            })
            .Build();
    }
}

public static class ToolResponses
{
    /// <summary>The loop hit its limit: the last message is still a function call nobody answered.</summary>
    public static bool EndedWithUnansweredCall(ChatResponse response)
    {
        // TODO-06: After MaximumIterationsPerRequest the client asks once more WITHOUT tools and returns what
        //          comes back, with no exception. Return true when the last message of the response still
        //          contains a FunctionCallContent.
        throw new NotImplementedException("TODO-06");
    }
}

public static class ApprovalFlow
{
    /// <summary>Wraps the function so the loop stops for approval, then answers the requests the approver decides.</summary>
    public static async Task<ChatResponse> RunAsync(
        IChatClient client, string prompt, AIFunction function, Func<ToolApprovalRequestContent, bool> approver)
    {
        // TODO-08: Wrap `function` in ApprovalRequiredAIFunction and send it in ChatOptions.Tools. When the
        //          response contains a ToolApprovalRequestContent, the tool has NOT run: append the response
        //          to the history, ask the approver, add a user message holding
        //          request.CreateResponse(approved, reason) and call the client again. Return the first
        //          response that has no approval request.
        await Task.CompletedTask;
        throw new NotImplementedException("TODO-08");
    }
}
