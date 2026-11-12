using Laraue.Apps.Identity.Internal.Contracts;
using Laraue.Apps.Identity.IntegrationTests.Infrastructure;
using Laraue.Apps.Identity.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DomainServiceId = Laraue.Apps.Identity.DataAccess.Entities.ServiceId;

namespace Laraue.Apps.Identity.IntegrationTests;

/// <summary>
/// Where a user came to a service from (BRD-295): kept on the user's row for that service, from the call that
/// created it.
/// </summary>
[Collection("IntegrationTest")]
public class SignUpSourceTests(InternalApiTestHost host) : IClassFixture<InternalApiTestHost>
{
    [Fact]
    public async Task CreateUserIfNotExists_ShouldStoreNormalizedSource_WhenUserComesToServiceFirstTime()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var response = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 7001,
            Source = " ProductHunt ",
        });

        Assert.Equal("producthunt", await GetSource(testScope, response.UserId, DomainServiceId.LaraueBoards));
        Assert.Equal("producthunt", response.Source);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldKeepFirstSource_WhenCalledAgainWithAnotherSource()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var response = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 7002,
            Source = "productradar",
        });
        var second = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 7002,
            Source = "producthunt",
        });

        Assert.Equal("productradar", await GetSource(testScope, response.UserId, DomainServiceId.LaraueBoards));
        // The caller gets the stored source back, not the one it just sent.
        Assert.Equal("productradar", second.Source);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldKeepSourcePerService_WhenUserComesToSecondServiceFromElsewhere()
    {
        using var testScope = host.CreateTestScope();
        var learnLanguage = host.CreateUserIdentityClient(ServiceId.LearnLanguage);
        var boards = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var response = await learnLanguage.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 7003,
            Source = "telegram",
        });
        await boards.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 7003,
            Source = "producthunt",
        });

        Assert.Equal("telegram", await GetSource(testScope, response.UserId, DomainServiceId.LearnLanguage));
        Assert.Equal("producthunt", await GetSource(testScope, response.UserId, DomainServiceId.LaraueBoards));
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldStoreNoSource_WhenCallerSendsNone()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var response = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest { TelegramId = 7004 });

        Assert.Null(await GetSource(testScope, response.UserId, DomainServiceId.LaraueBoards));
        Assert.False(response.HasSource);
    }

    [Fact]
    public async Task CreateUserIfNotExistsByGoogle_ShouldStoreSource_WhenUserComesToServiceFirstTime()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var response = await client.CreateUserIfNotExistsByGoogleAsync(new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "source-subject-1",
            Source = "habr",
        });

        Assert.Equal("habr", await GetSource(testScope, response.UserId, DomainServiceId.LaraueBoards));
        Assert.Equal("habr", response.Source);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("!!!", null)]
    [InlineData("Product Hunt!", "producthunt")]
    [InlineData("t.me", "t.me")]
    [InlineData("news_letter-2026", "news_letter-2026")]
    public void Normalize_ShouldKeepOnlyPlainCharacters_WhenSourceIsGiven(string? source, string? expected)
    {
        Assert.Equal(expected, SignUpSource.Normalize(source));
    }

    [Fact]
    public void Normalize_ShouldCutToMaxLength_WhenSourceIsLonger()
    {
        Assert.Equal(new string('a', SignUpSource.MaxLength), SignUpSource.Normalize(new string('a', 100)));
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldTakeSourceFromLandingLinkAndKeepReferrerHost_WhenCallerSendsRawData()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var response = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 7005,
            LandingUrl = "https://boards.laraue.com/ru?ref=ProductHunt&utm_source=newsletter",
            Referrer = "https://www.google.com/search?q=boards",
        });

        Assert.Equal("producthunt", response.Source);
        var row = await testScope.Database.UserServices
            .SingleAsync(x => x.UserId == Guid.Parse(response.UserId));
        Assert.Equal("producthunt", row.Source);
        Assert.Equal("www.google.com", row.ReferrerHost);
    }

    [Theory]
    [InlineData("invite", "https://boards.laraue.com/join/x?ref=habr", null, "invite")]
    [InlineData(null, "https://boards.laraue.com/?ref=habr&utm_source=newsletter", null, "habr")]
    [InlineData(null, "https://boards.laraue.com/?utm_source=Newsletter", null, "newsletter")]
    [InlineData(null, "https://boards.laraue.com/", "https://www.producthunt.com/posts/boards", "producthunt")]
    [InlineData(null, "https://boards.laraue.com/", "https://t.me/", "telegram")]
    [InlineData(null, "https://boards.laraue.com/", "https://www.google.de/", "google")]
    [InlineData(null, "https://boards.laraue.com/", "https://yandex.ru/search", "yandex")]
    [InlineData(null, "https://boards.laraue.com/", "https://blog.example.com/post?id=1", "blog.example.com")]
    [InlineData(null, "https://boards.laraue.com/login", "https://boards.laraue.com/", null)]
    [InlineData(null, "https://boards.laraue.com/", null, null)]
    [InlineData(null, "not a url", "also not a url", null)]
    public void Resolve_ShouldPickSourceByPrecedence_WhenCallerSendsWhatItSaw(
        string? source,
        string? landingUrl,
        string? referrer,
        string? expected)
    {
        Assert.Equal(expected, SignUpSource.Resolve(new SignUpAttribution(source, landingUrl, referrer), Options).Source);
    }

    [Fact]
    public void Resolve_ShouldKeepNoReferrerHost_WhenReferrerIsTheSameSite()
    {
        var resolved = SignUpSource.Resolve(
            new SignUpAttribution(null, "https://boards.laraue.com/login", "https://boards.laraue.com/ru"),
            Options);

        Assert.Null(resolved.ReferrerHost);
    }

    /// <summary>
    /// The lists the host loaded from its appsettings.json - so these tests check that configuration too.
    /// </summary>
    private SignUpSourceOptions Options => host.Services.GetRequiredService<IOptions<SignUpSourceOptions>>().Value;

    private static Task<string?> GetSource(InternalApiTestHostScope testScope, string userId, DomainServiceId serviceId)
    {
        return testScope.Database.UserServices
            .Where(x => x.UserId == Guid.Parse(userId) && x.ServiceId == serviceId)
            .Select(x => x.Source)
            .SingleAsync();
    }
}
