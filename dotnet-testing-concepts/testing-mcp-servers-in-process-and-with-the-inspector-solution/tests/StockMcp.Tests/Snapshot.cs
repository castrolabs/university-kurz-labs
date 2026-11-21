using System.Runtime.CompilerServices;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace StockMcp.Tests;

/// <summary>
/// The smallest snapshot test there is: compare text with a checked-in file. Verify (the snapshot lab) does this with
/// scrubbers and a diff tool; this one needs no license decision.
/// </summary>
internal static class Snapshot
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>What a host shows the model: name, description and input schema of every tool, sorted by name.</summary>
    internal static string RenderTools(IEnumerable<McpClientTool> tools) =>
        JsonSerializer.Serialize(
            tools.OrderBy(t => t.Name, StringComparer.Ordinal)
                 .Select(t => new { t.Name, t.Description, InputSchema = t.JsonSchema }),
            Indented).ReplaceLineEndings("\n");

    internal static void Verify(string actual, [CallerFilePath] string source = "", [CallerMemberName] string test = "")
    {
        var directory = Path.Combine(Path.GetDirectoryName(source)!, "snapshots");
        var verified = Path.Combine(directory, $"{test}.verified.txt");
        var received = Path.Combine(directory, $"{test}.received.txt");

        if (File.Exists(verified) && File.ReadAllText(verified).ReplaceLineEndings("\n") == actual)
        {
            File.Delete(received);
            return;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(received, actual);
        Assert.Fail($"Snapshot differs. Compare {received} with {verified}; rename it if the change is intended.");
    }
}
