using System.Runtime.CompilerServices;
using ModelContextProtocol.Client;

namespace StockMcp.Tests;

internal static class Snapshot
{
    /// <summary>What a host shows the model: name, description and input schema of every tool, sorted by name.</summary>
    internal static string RenderTools(IEnumerable<McpClientTool> tools)
    {
        // TODO-06 (part 1): Render the three fields a model reads, sorted by name, as indented JSON with "\n" line
        //   endings. Keep it to Name, Description and the input schema (McpClientTool.JsonSchema): anything else
        //   (timestamps, ids) would make the snapshot fail for no reason.
        throw new NotImplementedException("TODO-06: render the tools.");
    }

    internal static void Verify(string actual, [CallerFilePath] string source = "", [CallerMemberName] string test = "")
    {
        // TODO-05: The smallest snapshot test there is.
        //   - The files live in a "snapshots" folder next to the source file: {test}.verified.txt (checked in)
        //     and {test}.received.txt (written by a failing run, ignored by git).
        //   - When verified exists and has the same text as `actual` (compare with "\n" line endings), delete the
        //     received file, if any, and return.
        //   - Otherwise write `actual` to received (create the folder first) and fail the test with a message that
        //     names both files. The first run always fails; a human reads received and renames it to verified.
        throw new NotImplementedException("TODO-05: write Snapshot.Verify.");
    }
}
