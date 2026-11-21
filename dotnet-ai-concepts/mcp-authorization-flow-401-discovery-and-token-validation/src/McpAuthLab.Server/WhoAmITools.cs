using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;

namespace McpAuthLab.Server;

[McpServerToolType]
public sealed class WhoAmITools
{
    [McpServerTool(Name = "whoami"), Description("Echoes the subject, client and scope of the access token.")]
    public static string WhoAmI(ClaimsPrincipal user) =>
        $"sub={user.FindFirst("sub")?.Value} client_id={user.FindFirst("client_id")?.Value} scope={user.FindFirst("scope")?.Value}";
}
