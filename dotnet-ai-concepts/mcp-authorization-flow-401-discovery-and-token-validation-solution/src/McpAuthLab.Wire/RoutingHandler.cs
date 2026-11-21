namespace McpAuthLab;

/// <summary>Sends each request to the in-memory server that owns its port: the lab has no sockets.</summary>
public sealed class RoutingHandler(IReadOnlyDictionary<int, HttpMessageHandler> routes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var port = request.RequestUri!.Port;
        if (!routes.TryGetValue(port, out var handler))
        {
            throw new HttpRequestException($"Nothing listens on port {port}: {request.RequestUri}");
        }

        return new HttpMessageInvoker(handler, disposeHandler: false).SendAsync(request, ct);
    }
}
