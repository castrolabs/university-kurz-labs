using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace StockMcp;

public static class StockClient
{
    /// <summary>The text blocks of a tool result, joined with a space.</summary>
    public static string TextOf(CallToolResult result) =>
        string.Join(" ", result.Content.OfType<TextContentBlock>().Select(b => b.Text));

    /// <summary>
    /// The tools a model may see: only the allowed names, and the ones that need a human wrapped in
    /// <see cref="ApprovalRequiredAIFunction"/>.
    /// </summary>
    public static IList<AITool> ToolsForModel(
        IEnumerable<McpClientTool> tools, IReadOnlySet<string> allowed, IReadOnlySet<string> requireApproval) =>
        [.. tools
            .Where(t => allowed.Contains(t.Name))
            .Select(t => requireApproval.Contains(t.Name) ? (AITool)new ApprovalRequiredAIFunction(t) : t)];
}
