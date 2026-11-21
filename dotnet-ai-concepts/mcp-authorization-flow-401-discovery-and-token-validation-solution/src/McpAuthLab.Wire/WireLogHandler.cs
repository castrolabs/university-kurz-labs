namespace McpAuthLab;

/// <summary>Logs every request and response that passes through an <see cref="HttpClient"/>.</summary>
public sealed class WireLogHandler(WireLog log) : DelegatingHandler
{
    private static readonly string[] InterestingRequestHeaders = ["Authorization", "Mcp-Method"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var headers = InterestingRequestHeaders
            .Where(h => request.Headers.Contains(h))
            .Select(h => $" [{h}: {string.Join(",", request.Headers.GetValues(h))}]");
        log.Add(WireRedaction.Shorten($"--> {request.Method} {request.RequestUri}{string.Concat(headers)}"));

        if (request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded")
        {
            log.Add(WireRedaction.Shorten($"    form: {await request.Content.ReadAsStringAsync(ct)}"));
        }

        var response = await base.SendAsync(request, ct);

        var line = $"<-- {(int)response.StatusCode}";
        if (response.Headers.Location is { } location)
        {
            line += $" Location: {location}";
        }

        if (response.Headers.WwwAuthenticate.Count > 0)
        {
            line += $" WWW-Authenticate: {string.Join(", ", response.Headers.WwwAuthenticate)}";
        }

        log.Add(WireRedaction.Shorten(line));

        // text/event-stream bodies are never buffered: they may not end.
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            await response.Content.LoadIntoBufferAsync(ct);
            log.Add(WireRedaction.Shorten($"    {await response.Content.ReadAsStringAsync(ct)}"));
        }

        return response;
    }
}
