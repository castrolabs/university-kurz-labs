using System.Text.RegularExpressions;

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

public static partial class WireRedaction
{
    [GeneratedRegex(@"[A-Za-z0-9_\-.~]{40,}")]
    private static partial Regex LongValue();

    /// <summary>Tokens, codes and verifiers must not sit whole in a log: keep the first 8 characters of any run of 40 or more.</summary>
    public static string Shorten(string text) =>
        LongValue().Replace(text, m => m.Value[..8] + "...");
}
