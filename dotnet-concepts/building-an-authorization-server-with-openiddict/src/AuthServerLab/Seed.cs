using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServerLab;

public static class Seed
{
    public const string SpaRedirectUri = "http://localhost:6745/callback";

    // TODO-05: The two clients. "worker" is a confidential client (secret "worker-secret") that may
    // call the token and revocation endpoints and use ONLY the client credentials grant and the "orders"
    // scope. "spa" is a public client (no secret) with the single exact redirect URI SpaRedirectUri,
    // the authorization and token endpoints, the authorization code and refresh token grants, the
    // code response type, the scopes openid, email, profile, offline_access and orders, and the PKCE
    // requirement. Permissions are an allow-list: whatever is not listed is refused.
    public static IReadOnlyList<OpenIddictApplicationDescriptor> Applications()
    {
        throw new NotImplementedException();
    }

    // TODO-05 (second half): The scope "orders" implies the resource (the future "aud" of the access
    // token) "orders-api".
    public static IReadOnlyList<OpenIddictScopeDescriptor> ScopeDescriptors()
    {
        throw new NotImplementedException();
    }
}

/// <summary>Creates the tables and registers the clients and scopes when the host starts.</summary>
public sealed class SeedWorker(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DbContext>().Database.EnsureCreatedAsync(cancellationToken);

        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        foreach (var descriptor in Seed.Applications())
            if (await applications.FindByClientIdAsync(descriptor.ClientId!, cancellationToken) is null)
                await applications.CreateAsync(descriptor, cancellationToken);

        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var descriptor in Seed.ScopeDescriptors())
            if (await scopes.FindByNameAsync(descriptor.Name!, cancellationToken) is null)
                await scopes.CreateAsync(descriptor, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
