namespace McpAuthLab.Server;

/// <summary>
/// Why the server accepted or rejected a token. The 401 sent to the client is the same for every reason,
/// so this log is the only place the reason appears.
/// </summary>
public sealed class AuthDecisions
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries
    {
        get { lock (_entries) { return [.. _entries]; } }
    }

    public void Add(string entry)
    {
        lock (_entries) { _entries.Add(entry); }
    }
}
