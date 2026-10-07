using Laraue.Apps.Identity.Internal.Contracts;
using Laraue.Apps.Identity.IntegrationTests.Infrastructure;
using Laraue.Apps.Identity.InternalApiServices;
using Microsoft.Extensions.DependencyInjection;

namespace Laraue.Apps.Identity.IntegrationTests;

/// <summary>
/// Scrapes the host's Prometheus endpoint after real calls. Meters are process-wide, so another test can add
/// to the same series - assert that a series exists, not its exact count.
/// </summary>
[Collection("IntegrationTest")]
public class IdentityMetricsTests(InternalApiTestHost host) : IClassFixture<InternalApiTestHost>
{
    [Fact]
    public async Task Metrics_ShouldExposeRegistrationsBySourceAndService_WhenUsersSignUp()
    {
        using var testScope = host.CreateTestScope();
        var boards = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var learnLanguage = host.CreateUserIdentityClient(ServiceId.LearnLanguage);

        await boards.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest { TelegramId = 9001 });
        await learnLanguage.CreateUserIfNotExistsByGoogleAsync(
            new CreateUserIfNotExistsByGoogleRequest { GoogleSubject = "metrics-google-1" });

        var metrics = await ScrapeAsync();

        Assert.Contains(SeriesLines(metrics, "identity_users_registered_total"), line =>
            line.Contains("method=\"telegram\"") && line.Contains("service=\"LaraueBoards\""));
        Assert.Contains(SeriesLines(metrics, "identity_users_registered_total"), line =>
            line.Contains("method=\"google\"") && line.Contains("service=\"LearnLanguage\""));
        Assert.Contains(SeriesLines(metrics, "identity_service_users_added_total"), line =>
            line.Contains("service=\"LaraueBoards\""));
    }

    [Fact]
    public async Task Metrics_ShouldCountServiceUser_WhenExistingUserStartsUsingAnotherService()
    {
        using var testScope = host.CreateTestScope();
        var boards = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var learnLanguage = host.CreateUserIdentityClient(ServiceId.LearnLanguage);

        await boards.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest { TelegramId = 9002 });
        await learnLanguage.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest { TelegramId = 9002 });

        var metrics = await ScrapeAsync();

        Assert.Contains(SeriesLines(metrics, "identity_service_users_added_total"), line =>
            line.Contains("service=\"LearnLanguage\""));
    }

    [Fact]
    public async Task Metrics_ShouldExposeLinkedAccounts_WhenAccountIsLinked()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var user = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest { TelegramId = 9003 });

        await client.LinkGoogleAccountAsync(new LinkGoogleAccountRequest
        {
            UserId = user.UserId,
            GoogleSubject = "metrics-google-link",
        });

        var metrics = await ScrapeAsync();

        Assert.Contains(SeriesLines(metrics, "identity_accounts_linked_total"), line =>
            line.Contains("provider=\"google\"") && line.Contains("outcome=\"linked\""));
    }

    [Fact]
    public async Task StateMetrics_ShouldExposeTotalsBySourceAndService_WhenRefreshed()
    {
        using var testScope = host.CreateTestScope();
        var boards = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var learnLanguage = host.CreateUserIdentityClient(ServiceId.LearnLanguage);

        var both = await boards.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest { TelegramId = 9004 });
        await boards.LinkGoogleAccountAsync(new LinkGoogleAccountRequest
        {
            UserId = both.UserId,
            GoogleSubject = "metrics-google-both",
        });
        await boards.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest { TelegramId = 9005 });
        await learnLanguage.CreateUserIfNotExistsByGoogleAsync(
            new CreateUserIfNotExistsByGoogleRequest { GoogleSubject = "metrics-google-only" });

        await host.Services.GetRequiredService<IdentityStateMetrics>().RefreshAsync(CancellationToken.None);
        var metrics = await ScrapeAsync();

        Assert.Equal("3", GaugeValue(metrics, "identity_users"));
        Assert.Equal("1", GaugeValue(metrics, "identity_users_by_source", "source=\"telegram\""));
        Assert.Equal("1", GaugeValue(metrics, "identity_users_by_source", "source=\"google\""));
        Assert.Equal("1", GaugeValue(metrics, "identity_users_by_source", "source=\"both\""));
        Assert.Equal("2", GaugeValue(metrics, "identity_service_users", "service=\"LaraueBoards\""));
        Assert.Equal("1", GaugeValue(metrics, "identity_service_users", "service=\"LearnLanguage\""));
    }

    private async Task<string> ScrapeAsync() => await host.CreateClient().GetStringAsync("/_metrics");

    private static IEnumerable<string> SeriesLines(string metrics, string series)
        => metrics.Split('\n').Where(line => line.StartsWith(series + "{"));

    private static string GaugeValue(string metrics, string series, string? label = null)
    {
        var line = SeriesLines(metrics, series).Single(x => label is null || x.Contains(label));

        return line[(line.LastIndexOf(' ') + 1)..];
    }
}
