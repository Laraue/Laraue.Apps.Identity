using Grpc.Core.Interceptors;
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
    /// <see cref="UserIdentityService"/> the same way another Laraue app would - including going
    /// through <see cref="ServiceIdInterceptor"/> to attach <paramref name="callingService"/> as a
    /// header, same as a real caller's client would.
    /// </summary>
    public UserIdentityService.UserIdentityServiceClient CreateUserIdentityClient(ServiceId callingService)
    {
        var client = CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client,
        });

        var invoker = channel.Intercept(new ServiceIdInterceptor(callingService));

        return new UserIdentityService.UserIdentityServiceClient(invoker);
    }

    /// <summary>
    /// A client with no <see cref="ServiceIdInterceptor"/> attached - for exercising the "caller
    /// didn't identify itself" error path only, not something a real caller would ever construct.
    /// </summary>
    public UserIdentityService.UserIdentityServiceClient CreateUserIdentityClientWithoutServiceIdHeader()
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
