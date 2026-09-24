using Grpc.Core;
using Laraue.Apps.Identity.Internal.Contracts;
using Laraue.Apps.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Laraue.Apps.Identity.IntegrationTests;

[Collection("IntegrationTest")]
public class UserIdentityServiceTests(InternalApiTestHost host) : IClassFixture<InternalApiTestHost>
{
    [Fact]
    public async Task CreateUserIfNotExists_ShouldCreateNewUser_WhenTelegramIdIsUnknown()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var response = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 1,
        });

        Assert.True(Guid.TryParse(response.UserId, out var userId));
        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldReturnSameUserId_WhenCalledTwiceForSameTelegramId()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var first = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 2,
        });

        var second = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 2,
        });

        Assert.Equal(first.UserId, second.UserId);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldReturnSameUserId_WhenCalledForDifferentService()
    {
        using var testScope = host.CreateTestScope();
        var boardsClient = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var learnLanguageClient = host.CreateUserIdentityClient(ServiceId.LearnLanguage);

        var boards = await boardsClient.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 3,
        });

        var learnLanguage = await learnLanguageClient.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 3,
        });

        Assert.Equal(boards.UserId, learnLanguage.UserId);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldRefreshTelegramProfile_WhenCalledAgainWithChangedFields()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 4,
            TelegramUsername = "old_username",
            TelegramFirstName = "OldFirst",
        });

        await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 4,
            TelegramUsername = "new_username",
            TelegramFirstName = "NewFirst",
            TelegramLastName = "NewLast",
            TelegramLanguageCode = "en",
        });

        var account = await testScope.Database.TelegramAccounts.SingleAsync(x => x.TelegramId == 4);

        Assert.Equal("new_username", account.TelegramUserName);
        Assert.Equal("NewFirst", account.TelegramFirstName);
        Assert.Equal("NewLast", account.TelegramLastName);
        Assert.Equal("en", account.TelegramLanguageCode);
    }

    [Fact]
    public async Task CreateUserIfNotExists_ShouldReturnInvalidArgument_WhenServiceIdHeaderIsMissing()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClientWithoutServiceIdHeader();

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.CreateUserIfNotExistsAsync(
            new CreateUserIfNotExistsRequest { TelegramId = 5 }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task CreateUserIfNotExistsByGoogle_ShouldCreateNewUser_WhenGoogleSubjectIsUnknown()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var response = await client.CreateUserIfNotExistsByGoogleAsync(new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-1",
            Email = "user@example.com",
        });

        Assert.True(Guid.TryParse(response.UserId, out var userId));
        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task CreateUserIfNotExistsByGoogle_ShouldReturnSameUserId_WhenCalledTwiceForSameGoogleSubject()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var first = await client.CreateUserIfNotExistsByGoogleAsync(new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-2",
        });

        var second = await client.CreateUserIfNotExistsByGoogleAsync(new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-2",
        });

        Assert.Equal(first.UserId, second.UserId);
    }

    [Fact]
    public async Task CreateUserIfNotExistsByGoogle_ShouldReturnSameUserId_WhenCalledForDifferentService()
    {
        using var testScope = host.CreateTestScope();
        var boardsClient = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var learnLanguageClient = host.CreateUserIdentityClient(ServiceId.LearnLanguage);

        var boards = await boardsClient.CreateUserIfNotExistsByGoogleAsync(new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-3",
        });

        var learnLanguage = await learnLanguageClient.CreateUserIfNotExistsByGoogleAsync(
            new CreateUserIfNotExistsByGoogleRequest
            {
                GoogleSubject = "google-3",
            });

        Assert.Equal(boards.UserId, learnLanguage.UserId);

        var userId = Guid.Parse(boards.UserId);
        var recordedServicesCount = await testScope.Database.UserServices.CountAsync(x => x.UserId == userId);

        Assert.Equal(2, recordedServicesCount);
    }

    [Fact]
    public async Task CreateUserIfNotExistsByGoogle_ShouldRefreshGoogleProfile_WhenCalledAgainWithChangedFields()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        await client.CreateUserIfNotExistsByGoogleAsync(new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-4",
            Email = "old@example.com",
            Name = "Old Name",
        });

        await client.CreateUserIfNotExistsByGoogleAsync(new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-4",
            Email = "new@example.com",
            Name = "New Name",
            GivenName = "New",
            FamilyName = "Name",
        });

        var account = await testScope.Database.GoogleAccounts.SingleAsync(x => x.GoogleSubject == "google-4");

        Assert.Equal("new@example.com", account.Email);
        Assert.Equal("New Name", account.Name);
        Assert.Equal("New", account.GivenName);
        Assert.Equal("Name", account.FamilyName);
    }

    [Fact]
    public async Task CreateUserIfNotExistsByGoogle_ShouldCreateDifferentUser_WhenSameUserAlreadyLoggedInViaTelegram()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var telegram = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 6,
        });

        var google = await client.CreateUserIfNotExistsByGoogleAsync(new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-6",
        });

        Assert.NotEqual(telegram.UserId, google.UserId);
    }

    [Fact]
    public async Task CreateUserIfNotExistsByGoogle_ShouldReturnInvalidArgument_WhenGoogleSubjectIsEmpty()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.CreateUserIfNotExistsByGoogleAsync(
            new CreateUserIfNotExistsByGoogleRequest { GoogleSubject = "" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task CreateUserIfNotExistsByGoogle_ShouldReturnInvalidArgument_WhenServiceIdHeaderIsMissing()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClientWithoutServiceIdHeader();

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.CreateUserIfNotExistsByGoogleAsync(
            new CreateUserIfNotExistsByGoogleRequest { GoogleSubject = "google-7" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }
}
