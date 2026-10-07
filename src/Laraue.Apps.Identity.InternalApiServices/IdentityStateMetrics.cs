using System.Diagnostics.Metrics;
using Laraue.Apps.Identity.DataAccess;
using Laraue.Apps.Identity.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Laraue.Apps.Identity.InternalApiServices;

/// <summary>
/// Gauges of Identity's state, read from the database: how many users there are now and how they split between
/// sign-in sources and products. They answer "what is true now" and survive a restart, which event counters
/// cannot - plot them over time to see the totals grow. Refreshed in the background, a scrape only reads the last
/// snapshot. With several replicas each publishes the same numbers, so query them with <c>max()</c>.
/// </summary>
public sealed class IdentityStateMetrics : BackgroundService
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    public const string SourceTelegram = "telegram";
    public const string SourceGoogle = "google";
    public const string SourceBoth = "both";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdentityStateMetrics> _logger;

    private volatile Snapshot _snapshot = Snapshot.Empty;

    public IdentityStateMetrics(
        IMeterFactory meterFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<IdentityStateMetrics> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var meter = meterFactory.Create(Services.Metrics.IdentityMetrics.MeterName);

        meter.CreateObservableGauge(
            "identity.users",
            () => _snapshot.Users,
            description: "Global users now (users absorbed into another user by an account move are not counted).");

        meter.CreateObservableGauge(
            "identity.users.by_source",
            () => _snapshot.UsersBySource.Select(x => new Measurement<long>(
                x.Value,
                new KeyValuePair<string, object?>("source", x.Key))),
            description: "Users by the accounts they have: telegram only, google only, or both.");

        meter.CreateObservableGauge(
            "identity.service.users",
            () => _snapshot.UsersByService.Select(x => new Measurement<long>(
                x.Value,
                new KeyValuePair<string, object?>("service", x.Key.ToString()))),
            description: "Users that use each service. A user of several services is counted in each.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        do
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The gauges keep their last values; the next tick tries again.
                _logger.LogWarning(ex, "Refreshing the identity state metrics failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var users = context.Users.Where(x => x.MergedIntoUserId == null);

        var total = await users.LongCountAsync(cancellationToken);
        var telegramOnly = await users.LongCountAsync(
            x => x.TelegramAccount != null && x.GoogleAccount == null, cancellationToken);
        var googleOnly = await users.LongCountAsync(
            x => x.TelegramAccount == null && x.GoogleAccount != null, cancellationToken);
        var both = await users.LongCountAsync(
            x => x.TelegramAccount != null && x.GoogleAccount != null, cancellationToken);

        var byService = await context.UserServices
            .Where(x => x.User!.MergedIntoUserId == null)
            .GroupBy(x => x.ServiceId)
            .Select(x => new { ServiceId = x.Key, Count = x.LongCount() })
            .ToListAsync(cancellationToken);

        _snapshot = new Snapshot(
            total,
            new Dictionary<string, long>
            {
                [SourceTelegram] = telegramOnly,
                [SourceGoogle] = googleOnly,
                [SourceBoth] = both,
            },
            byService.ToDictionary(x => x.ServiceId, x => x.Count));
    }

    private sealed record Snapshot(
        long Users,
        IReadOnlyDictionary<string, long> UsersBySource,
        IReadOnlyDictionary<ServiceId, long> UsersByService)
    {
        public static readonly Snapshot Empty = new(0, new Dictionary<string, long>(), new Dictionary<ServiceId, long>());
    }
}
