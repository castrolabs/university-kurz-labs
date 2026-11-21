using Microsoft.Agents.AI;

namespace AgentLab;

public static class Conversation
{
    /// <summary>Asks every question in order; with a session the agent keeps the history, without one each call stands alone.</summary>
    public static async Task<AgentSession?> AskAllAsync(AIAgent agent, IEnumerable<string> questions, bool useSession)
    {
        AgentSession? session = useSession ? await agent.CreateSessionAsync() : null;
        foreach (var question in questions)
        {
            await agent.RunAsync(question, session);
        }

        return session;
    }

    public static async Task<string> SerializeAsync(AIAgent agent, AgentSession session) =>
        (await agent.SerializeSessionAsync(session)).GetRawText();

    public static async Task<AgentSession> RestoreAsync(AIAgent agent, string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return await agent.DeserializeSessionAsync(document.RootElement.Clone());
    }
}

/// <summary>Runs on one session never overlap: a per-session lock.</summary>
public sealed class SerializedAgent(AIAgent agent)
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<AgentSession, SemaphoreSlim> _locks = new();

    public async Task<AgentResponse> RunAsync(AgentSession session, string message)
    {
        var gate = _locks.GetValue(session, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            return await agent.RunAsync(message, session);
        }
        finally
        {
            gate.Release();
        }
    }
}

public static class Approvals
{
    public static async Task<AgentResponse> RunWithApprovalAsync(AIAgent agent, AgentSession session, string message, bool approve)
    {
        var first = await agent.RunAsync(message, session);
        var request = first.Messages.SelectMany(m => m.Contents).OfType<Microsoft.Extensions.AI.ToolApprovalRequestContent>().FirstOrDefault();
        if (request is null)
        {
            return first;
        }

        var answer = new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.User, [request.CreateResponse(approve)]);
        return await agent.RunAsync(answer, session);
    }
}
