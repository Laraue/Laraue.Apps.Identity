using Grpc.Net.Client;
using Laraue.Apps.Identity.DataAccess;
using Laraue.Apps.Identity.Internal.Contracts;
using Laraue.Apps.Identity.InternalApiHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Laraue.Apps.Identity.IntegrationTests.Infrastructure;

public class InternalApiTestHost : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddJsonFile("appsettings.json", optional: true);
        });

        return base.CreateHost(builder);
    }

    /// <summary>
    /// A gRPC client wired to this in-memory test server (no real network socket), for calling
    /// <see cref="UserIdentityService"/> the same way another Laraue app would.
    /// </summary>
    public UserIdentityService.UserIdentityServiceClient CreateUserIdentityClient()
    {
        var client = CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client,
        });

        return new UserIdentityService.UserIdentityServiceClient(channel);
    }

    public void CleanDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        db.CleanDatabase();
    }
}
