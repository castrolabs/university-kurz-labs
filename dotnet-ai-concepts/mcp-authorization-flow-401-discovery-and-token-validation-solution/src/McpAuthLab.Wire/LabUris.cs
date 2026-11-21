namespace McpAuthLab;

/// <summary>
/// The three addresses of the lab. Nothing listens on them: the tests route each one to an in-memory
/// server, so the URLs are real strings in real requests, with no socket behind them.
/// </summary>
public static class LabUris
{
    /// <summary>The canonical URI of the MCP endpoint: the audience of the tokens and the OAuth `resource`.</summary>
    public const string McpEndpoint = "http://localhost:5900/mcp";

    public const string McpOrigin = "http://localhost:5900";

    public const string AuthorizationServer = "https://localhost:5901";

    public const string ClientCallback = "http://localhost:5902/callback";

    public const string ClientId = "demo-client";

    public const string ClientSecret = "demo-secret";

    public const string Scope = "mcp:tools";
}
