using Laraue.Apps.Identity.Internal.Contracts;
using Laraue.Apps.Identity.IntegrationTests.Infrastructure;
using Laraue.Apps.Identity.Services;
using Microsoft.EntityFrameworkCore;
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
        await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 7002,
            Source = "producthunt",
        });

        Assert.Equal("productradar", await GetSource(testScope, response.UserId, DomainServiceId.LaraueBoards));
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

    private static Task<string?> GetSource(InternalApiTestHostScope testScope, string userId, DomainServiceId serviceId)
    {
        return testScope.Database.UserServices
            .Where(x => x.UserId == Guid.Parse(userId) && x.ServiceId == serviceId)
            .Select(x => x.Source)
            .SingleAsync();
    }
}
