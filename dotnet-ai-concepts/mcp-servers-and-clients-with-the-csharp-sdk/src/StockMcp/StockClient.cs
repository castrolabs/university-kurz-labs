using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace StockMcp;

public static class StockClient
{
    /// <summary>The text blocks of a tool result, joined with a space.</summary>
    public static string TextOf(CallToolResult result)
    {
        // TODO-07: Join the text of every TextContentBlock in result.Content with a single space.
        throw new NotImplementedException();
    }

    /// <summary>The tools a model may see: allowed names only, the risky ones behind a human.</summary>
    public static IList<AITool> ToolsForModel(
        IEnumerable<McpClientTool> tools, IReadOnlySet<string> allowed, IReadOnlySet<string> requireApproval)
    {
        // TODO-08: Keep only the tools whose Name is in allowed. A tool whose name is in requireApproval
        //          goes into the list wrapped so that function invocation asks before running it; the rest
        //          go in as they are (McpClientTool is already an AIFunction).
        throw new NotImplementedException();
    }
}
