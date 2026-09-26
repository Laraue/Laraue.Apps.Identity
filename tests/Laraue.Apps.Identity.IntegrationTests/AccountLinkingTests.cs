using Grpc.Core;
using Laraue.Apps.Identity.Internal.Contracts;
using Laraue.Apps.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Laraue.Apps.Identity.IntegrationTests;

[Collection("IntegrationTest")]
public class AccountLinkingTests(InternalApiTestHost host) : IClassFixture<InternalApiTestHost>
{
    [Fact]
    public async Task LinkTelegramAccount_ShouldLinkAccountToUser_WhenAccountIsNew()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var googleUserId = await CreateGoogleUserAsync(client, "google-1");

        var response = await client.LinkTelegramAccountAsync(new LinkTelegramAccountRequest
        {
            UserId = googleUserId,
            TelegramId = 101,
            TelegramUsername = "ada",
        });

        Assert.Equal(LinkAccountResult.Linked, response.Result);
        Assert.False(response.HasPreviousUserId);
        Assert.Equal(googleUserId, await CreateTelegramUserAsync(client, 101));
    }

    [Fact]
    public async Task LinkTelegramAccount_ShouldReturnLinked_WhenAccountIsAlreadyLinkedToSameUser()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var userId = await CreateTelegramUserAsync(client, 102);

        var response = await client.LinkTelegramAccountAsync(new LinkTelegramAccountRequest
        {
            UserId = userId,
            TelegramId = 102,
        });

        Assert.Equal(LinkAccountResult.Linked, response.Result);
    }

    [Fact]
    public async Task LinkTelegramAccount_ShouldMoveAccount_WhenOwnerIsUsedOnlyByCallingService()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var telegramUserId = await CreateTelegramUserAsync(client, 104);
        var googleUserId = await CreateGoogleUserAsync(client, "google-4");

        var response = await client.LinkTelegramAccountAsync(new LinkTelegramAccountRequest
        {
            UserId = googleUserId,
            TelegramId = 104,
        });

        Assert.Equal(LinkAccountResult.Moved, response.Result);
        Assert.Equal(telegramUserId, response.PreviousUserId);
        Assert.Equal(googleUserId, await CreateTelegramUserAsync(client, 104));
    }

    [Fact]
    public async Task LinkTelegramAccount_ShouldLeaveAccount_WhenOwnerIsUsedByAnotherService()
    {
        using var testScope = host.CreateTestScope();
        var boardsClient = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var learnLanguageClient = host.CreateUserIdentityClient(ServiceId.LearnLanguage);
        var telegramUserId = await CreateTelegramUserAsync(learnLanguageClient, 105);
        var googleUserId = await CreateGoogleUserAsync(boardsClient, "google-5");

        var response = await boardsClient.LinkTelegramAccountAsync(new LinkTelegramAccountRequest
        {
            UserId = googleUserId,
            TelegramId = 105,
        });

        Assert.Equal(LinkAccountResult.OwnerUsedByAnotherService, response.Result);
        Assert.Equal(telegramUserId, response.PreviousUserId);
    }

    [Fact]
    public async Task LinkTelegramAccount_ShouldRefuse_WhenUserAlreadyHasAnotherTelegramAccount()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var userId = await CreateTelegramUserAsync(client, 106);

        var response = await client.LinkTelegramAccountAsync(new LinkTelegramAccountRequest
        {
            UserId = userId,
            TelegramId = 107,
        });

        Assert.Equal(LinkAccountResult.UserHasOtherAccount, response.Result);
        Assert.False(await testScope.Database.TelegramAccounts.AnyAsync(x => x.TelegramId == 107));
    }

    [Fact]
    public async Task LinkTelegramAccount_ShouldReturnNotFound_WhenUserIsUnknown()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.LinkTelegramAccountAsync(
            new LinkTelegramAccountRequest { UserId = Guid.NewGuid().ToString(), TelegramId = 108 }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task LinkTelegramAccount_ShouldReturnInvalidArgument_WhenUserIdIsNotGuid()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.LinkTelegramAccountAsync(
            new LinkTelegramAccountRequest { UserId = "not-a-guid", TelegramId = 109 }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task LinkGoogleAccount_ShouldLinkAccountToUser_WhenAccountIsNew()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var telegramUserId = await CreateTelegramUserAsync(client, 110);

        var response = await client.LinkGoogleAccountAsync(new LinkGoogleAccountRequest
        {
            UserId = telegramUserId,
            GoogleSubject = "google-10",
            Email = "ada@example.com",
        });

        Assert.Equal(LinkAccountResult.Linked, response.Result);
        Assert.Equal(telegramUserId, await CreateGoogleUserAsync(client, "google-10"));
    }

    [Fact]
    public async Task LinkGoogleAccount_ShouldMoveAccount_WhenOwnerIsUsedOnlyByCallingService()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var telegramUserId = await CreateTelegramUserAsync(client, 111);
        var googleUserId = await CreateGoogleUserAsync(client, "google-11");

        var response = await client.LinkGoogleAccountAsync(new LinkGoogleAccountRequest
        {
            UserId = telegramUserId,
            GoogleSubject = "google-11",
        });

        Assert.Equal(LinkAccountResult.Moved, response.Result);
        Assert.Equal(googleUserId, response.PreviousUserId);
        Assert.Equal(telegramUserId, await CreateGoogleUserAsync(client, "google-11"));
    }

    [Fact]
    public async Task LinkGoogleAccount_ShouldRefuse_WhenUserAlreadyHasAnotherGoogleAccount()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var userId = await CreateGoogleUserAsync(client, "google-12");

        var response = await client.LinkGoogleAccountAsync(new LinkGoogleAccountRequest
        {
            UserId = userId,
            GoogleSubject = "google-13",
        });

        Assert.Equal(LinkAccountResult.UserHasOtherAccount, response.Result);
    }

    [Fact]
    public async Task LinkGoogleAccount_ShouldReturnInvalidArgument_WhenGoogleSubjectIsEmpty()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var userId = await CreateTelegramUserAsync(client, 114);

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.LinkGoogleAccountAsync(
            new LinkGoogleAccountRequest { UserId = userId, GoogleSubject = "" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    private static async Task<string> CreateTelegramUserAsync(UserIdentityService.UserIdentityServiceClient client, long telegramId)
    {
        var response = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest { TelegramId = telegramId });
        return response.UserId;
    }

    private static async Task<string> CreateGoogleUserAsync(UserIdentityService.UserIdentityServiceClient client, string googleSubject)
    {
        var response = await client.CreateUserIfNotExistsByGoogleAsync(
            new CreateUserIfNotExistsByGoogleRequest { GoogleSubject = googleSubject });
        return response.UserId;
    }
}
