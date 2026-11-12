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

    /// <summary>
    /// A service's most common sign-up sources get their own label value, the rest are <see cref="SignUpSourceOther"/>:
    /// sources are free values from links, so they can't all be labels.
    /// </summary>
    public const int MaxSignUpSourcesPerService = 10;

    public const string SignUpSourceOther = "other";
    public const string SignUpSourceNone = "none";

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

        meter.CreateObservableGauge(
            "identity.service.users.by_sign_up_source",
            () => _snapshot.UsersBySignUpSource.Select(x => new Measurement<long>(
                x.Count,
                new KeyValuePair<string, object?>("service", x.ServiceId.ToString()),
                new KeyValuePair<string, object?>("source", x.Source))),
            description: "Users of each service by where they came to it from (UserService.Source, BRD-295): the " +
                $"service's {MaxSignUpSourcesPerService} most common sources, {SignUpSourceOther} for the rest, " +
                $"{SignUpSourceNone} without one (all users from before BRD-295).");
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

        var bySignUpSource = await context.UserServices
            .Where(x => x.User!.MergedIntoUserId == null)
            .GroupBy(x => new { x.ServiceId, x.Source })
            .Select(x => new SignUpSourceCount(x.Key.ServiceId, x.Key.Source ?? SignUpSourceNone, x.LongCount()))
            .ToListAsync(cancellationToken);

        _snapshot = new Snapshot(
            total,
            new Dictionary<string, long>
            {
                [SourceTelegram] = telegramOnly,
                [SourceGoogle] = googleOnly,
                [SourceBoth] = both,
            },
            byService.ToDictionary(x => x.ServiceId, x => x.Count),
            GroupSignUpSources(bySignUpSource));
    }

    /// <summary>
    /// Keeps each service's <see cref="MaxSignUpSourcesPerService"/> most common sources and adds the rest up as
    /// <see cref="SignUpSourceOther"/>. Every service always has <see cref="SignUpSourceOther"/> and
    /// <see cref="SignUpSourceNone"/>, zero or not, so the series exist from the first scrape.
    /// </summary>
    private static SignUpSourceCount[] GroupSignUpSources(IEnumerable<SignUpSourceCount> counts)
    {
        var byService = counts.ToLookup(x => x.ServiceId);

        return Enum.GetValues<ServiceId>()
            .SelectMany(serviceId =>
            {
                var sources = byService[serviceId]
                    .Where(x => x.Source != SignUpSourceNone)
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Source, StringComparer.Ordinal)
                    .ToArray();

                return sources
                    .Take(MaxSignUpSourcesPerService)
                    .Append(new SignUpSourceCount(
                        serviceId,
                        SignUpSourceOther,
                        sources
                            .Skip(MaxSignUpSourcesPerService)
                            .Sum(x => x.Count)))
                    .Append(new SignUpSourceCount(
                        serviceId,
                        SignUpSourceNone,
                        byService[serviceId]
                            .Where(x => x.Source == SignUpSourceNone)
                            .Sum(x => x.Count)));
            })
            .ToArray();
    }

    private sealed record SignUpSourceCount(ServiceId ServiceId, string Source, long Count);

    private sealed record Snapshot(
        long Users,
        IReadOnlyDictionary<string, long> UsersBySource,
        IReadOnlyDictionary<ServiceId, long> UsersByService,
        IReadOnlyList<SignUpSourceCount> UsersBySignUpSource)
    {
        public static readonly Snapshot Empty = new(
            0,
            new Dictionary<string, long>(),
            new Dictionary<ServiceId, long>(),
            []);
    }
}
