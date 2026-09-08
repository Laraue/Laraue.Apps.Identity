using Laraue.Apps.Identity.DataAccess;
using Laraue.Apps.Identity.Internal.Contracts;
using Laraue.Apps.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Laraue.Apps.Identity.IntegrationTests;

public class UserIdentityServiceTests(InternalApiTestHost host) : IClassFixture<InternalApiTestHost>
{
    [Fact]
    public async Task CreateUserIfNotExists_ShouldCreateNewUser_WhenTelegramIdIsUnknown()
    {
        host.CleanDatabase();
        var client = host.CreateUserIdentityClient();

        var response = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 1,
            ServiceId = ServiceId.LaraueBoards,
        });

        Assert.True(Guid.TryParse(response.UserId, out var userId));
        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldReturnSameUserId_WhenCalledTwiceForSameTelegramId()
    {
        host.CleanDatabase();
        var client = host.CreateUserIdentityClient();

        var first = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 2,
            ServiceId = ServiceId.LaraueBoards,
        });

        var second = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 2,
            ServiceId = ServiceId.LaraueBoards,
        });

        Assert.Equal(first.UserId, second.UserId);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldReturnSameUserId_WhenCalledForDifferentService()
    {
        host.CleanDatabase();
        var client = host.CreateUserIdentityClient();

        var boards = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 3,
            ServiceId = ServiceId.LaraueBoards,
        });

        var learnLanguage = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 3,
            ServiceId = ServiceId.LearnLanguage,
        });

        Assert.Equal(boards.UserId, learnLanguage.UserId);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldRefreshTelegramProfile_WhenCalledAgainWithChangedFields()
    {
        host.CleanDatabase();
        var client = host.CreateUserIdentityClient();

        await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 4,
            ServiceId = ServiceId.LaraueBoards,
            TelegramUsername = "old_username",
            TelegramFirstName = "OldFirst",
        });

        await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 4,
            ServiceId = ServiceId.LaraueBoards,
            TelegramUsername = "new_username",
            TelegramFirstName = "NewFirst",
            TelegramLastName = "NewLast",
            TelegramLanguageCode = "en",
        });

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var account = await db.TelegramAccounts.SingleAsync(x => x.TelegramId == 4);

        Assert.Equal("new_username", account.TelegramUserName);
        Assert.Equal("NewFirst", account.TelegramFirstName);
        Assert.Equal("NewLast", account.TelegramLastName);
        Assert.Equal("en", account.TelegramLanguageCode);
    }
}
