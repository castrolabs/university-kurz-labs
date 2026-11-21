#pragma warning disable CS9113 // parameters are used once TODO-03 is done
using Microsoft.Agents.AI;

namespace AgentLab;

public static class Conversation
{
    public static async Task<AgentSession?> AskAllAsync(AIAgent agent, IEnumerable<string> questions, bool useSession)
    {
        // TODO-01: Ask every question in order with agent.RunAsync. When useSession is true create ONE session
        //          (agent.CreateSessionAsync) and pass it to every call, then return it; when false pass no
        //          session (each call stands alone) and return null.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static async Task<string> SerializeAsync(AIAgent agent, AgentSession session)
    {
        // TODO-02: Serialize the session to JSON text (agent.SerializeSessionAsync gives a JsonElement).
        //          Check what the JSON holds: the messages, but not the instructions or the tools.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static async Task<AgentSession> RestoreAsync(AIAgent agent, string json)
    {
        // TODO-02: Rebuild a session from that JSON with agent.DeserializeSessionAsync, on a NEW agent
        //          configured the same way (parse the text with JsonDocument, clone the root element).
        throw new NotImplementedException("Not implemented yet.");
    }
}

/// <summary>Runs on one session never overlap.</summary>
public sealed class SerializedAgent(AIAgent agent)
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<AgentSession, SemaphoreSlim> _locks = new();

    public Task<AgentResponse> RunAsync(AgentSession session, string message)
    {
        // TODO-03: Two RunAsync calls at once on the same session both read the history before either writes
        //          it: nothing throws, but replies end up in the wrong order. Take a per-session lock
        //          (_locks.GetValue(session, _ => new SemaphoreSlim(1, 1)); WaitAsync before, Release in a
        //          finally) around agent.RunAsync(message, session). Different sessions must not block each other.
        throw new NotImplementedException("Not implemented yet.");
    }
}

public static class Approvals
{
    public static Task<AgentResponse> RunWithApprovalAsync(AIAgent agent, AgentSession session, string message, bool approve)
    {
        // TODO-06: Run the message on the session. If the response contains a ToolApprovalRequestContent
        //          (look through response.Messages[*].Contents), answer it with request.CreateResponse(approve)
        //          wrapped in a user ChatMessage and run THAT on the same session; return the second response.
        //          With no approval request, return the first response. The model only saw call and result.
        throw new NotImplementedException("Not implemented yet.");
    }
}
