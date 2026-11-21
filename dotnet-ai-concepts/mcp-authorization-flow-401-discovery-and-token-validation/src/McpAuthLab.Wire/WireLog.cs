namespace McpAuthLab;

/// <summary>What the student reads: one line per request and response, in the order they happened.</summary>
public sealed class WireLog
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get { lock (_lines) { return [.. _lines]; } }
    }

    public void Add(string line)
    {
        lock (_lines) { _lines.Add(line); }
    }

    public override string ToString() => string.Join(Environment.NewLine, Lines);
}

public static class WireRedaction
{
    /// <summary>
    /// Tokens, codes and verifiers must not sit whole in a log.
    /// </summary>
    public static string Shorten(string text)
    {
        // TODO-07: Keep only the first 8 characters, followed by "...", of every run of 40 or more characters
        //          made of letters, digits, '_', '-', '.' and '~' (JWTs, codes, PKCE values). Shorter text,
        //          and every separator (":", "/", "=", "&", quotes, spaces), stays as it is.
        return text;
    }
}
