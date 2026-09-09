using Laraue.Apps.Identity.DataAccess;
using Laraue.Apps.Identity.InternalApiServices;
using Laraue.Grpc.OpenTelemetry;
using Laraue.Grpc.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;

namespace Laraue.Apps.Identity.InternalApiHost;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        const string dbConnectionStringName = "Postgre";

        var grpcPort = builder.Configuration.GetValue<int?>("Kestrel:GrpcPort") ?? 5363;
        var healthPort = builder.Configuration.GetValue<int?>("Kestrel:HealthPort") ?? 5364;

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
            options.ListenAnyIP(healthPort, listen => listen.Protocols = HttpProtocols.Http1);
        });

        var connection = builder.Configuration.GetConnectionString(dbConnectionStringName);
        builder.Services.AddDbContext<DatabaseContext>(opt => opt
            .UseNpgsql(connection)
            .UseSnakeCaseNamingConvention());

        builder.Services.AddInternalApiServices();

        builder.Services
            .AddGrpc()
            .AddLaraueGrpcTelemetry();

        builder.Services.AddLaraueGrpcTelemetry(
            configureMetrics: metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        builder.Services.AddHealthChecks();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            await db.Database.MigrateAsync();
        }

        app.MapGrpcService<UserIdentityGrpcService>();
        app.MapHealthChecks("/_health");
        app.MapPrometheusScrapingEndpoint("/_metrics");

        await app.RunAsync();
    }
}
