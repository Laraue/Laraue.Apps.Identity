using Grpc.Core;
using Laraue.Apps.Identity.Internal.Contracts;
using Laraue.Apps.Identity.IntegrationTests.Infrastructure;

namespace Laraue.Apps.Identity.IntegrationTests;

[Collection("IntegrationTest")]
public class UserProfileTests(InternalApiTestHost host) : IClassFixture<InternalApiTestHost>
{
    [Fact]
    public async Task GetUserProfile_ShouldReturnTelegramNames_WhenUserWasCreatedWithTelegram()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var created = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 201,
            TelegramUsername = "ada",
            TelegramFirstName = "Ada",
            TelegramLastName = "Lovelace",
        });

        var profile = await client.GetUserProfileAsync(new GetUserProfileRequest { UserId = created.UserId });

        Assert.Equal("ada", profile.UserName);
        Assert.Equal("Ada", profile.GivenName);
        Assert.Equal("Lovelace", profile.FamilyName);
        Assert.Equal("ada", profile.DisplayName);
        Assert.Equal("AD", profile.Initials);
    }

    [Fact]
    public async Task GetUserProfile_ShouldReturnGoogleNames_WhenUserWasCreatedWithGoogle()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var userId = await CreateGoogleUserAsync(client, new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-202",
            Email = "ada@example.com",
            Name = "Ada Lovelace",
            GivenName = "Ada",
            FamilyName = "Lovelace",
        });

        var profile = await client.GetUserProfileAsync(new GetUserProfileRequest { UserId = userId });

        Assert.False(profile.HasUserName);
        Assert.Equal("Ada", profile.GivenName);
        Assert.Equal("Lovelace", profile.FamilyName);
        Assert.Equal("Ada Lovelace", profile.DisplayName);
        Assert.Equal("AL", profile.Initials);
    }

    [Fact]
    public async Task GetUserProfile_ShouldReturnFullNameAsGivenName_WhenGoogleAccountHasNoSplitName()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var userId = await CreateGoogleUserAsync(client, new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-203",
            Email = "ada@example.com",
            Name = "Ada Lovelace",
        });

        var profile = await client.GetUserProfileAsync(new GetUserProfileRequest { UserId = userId });

        Assert.Equal("Ada Lovelace", profile.GivenName);
        Assert.False(profile.HasFamilyName);
        Assert.Equal("Ada Lovelace", profile.DisplayName);
        Assert.Equal("AD", profile.Initials);
    }

    [Fact]
    public async Task GetUserProfile_ShouldReturnEmailLocalPartAsGivenName_WhenGoogleAccountHasOnlyEmail()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var userId = await CreateGoogleUserAsync(client, new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-204",
            Email = "ada.lovelace@example.com",
        });

        var profile = await client.GetUserProfileAsync(new GetUserProfileRequest { UserId = userId });

        Assert.Equal("ada.lovelace", profile.GivenName);
        Assert.Equal("ada.lovelace", profile.DisplayName);
        Assert.Equal("AD", profile.Initials);
    }

    [Fact]
    public async Task GetUserProfile_ShouldCutNames_WhenGoogleNamesAreLongerThanMaxLength()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var userId = await CreateGoogleUserAsync(client, new CreateUserIfNotExistsByGoogleRequest
        {
            GoogleSubject = "google-207",
            GivenName = new string('a', 200),
            FamilyName = new string('b', 200),
        });

        var profile = await client.GetUserProfileAsync(new GetUserProfileRequest { UserId = userId });

        Assert.Equal(new string('a', 128), profile.GivenName);
        Assert.Equal(new string('b', 128), profile.FamilyName);
        Assert.Equal($"{new string('a', 128)} {new string('b', 128)}", profile.DisplayName);
    }

    [Fact]
    public async Task GetUserProfile_ShouldKeepFirstAccountNames_WhenAnotherAccountIsLinked()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var created = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 205,
            TelegramFirstName = "Ada",
        });
        await client.LinkGoogleAccountAsync(new LinkGoogleAccountRequest
        {
            UserId = created.UserId,
            GoogleSubject = "google-205",
            GivenName = "Augusta",
            FamilyName = "Lovelace",
        });

        var profile = await client.GetUserProfileAsync(new GetUserProfileRequest { UserId = created.UserId });

        Assert.Equal("Ada", profile.GivenName);
        Assert.False(profile.HasFamilyName);
        Assert.Equal("Ada", profile.DisplayName);
    }

    [Fact]
    public async Task GetUserProfile_ShouldKeepNames_WhenAccountProfileIsRefreshed()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);
        var created = await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 206,
            TelegramFirstName = "Ada",
        });
        await client.CreateUserIfNotExistsAsync(new CreateUserIfNotExistsRequest
        {
            TelegramId = 206,
            TelegramFirstName = "Augusta",
        });

        var profile = await client.GetUserProfileAsync(new GetUserProfileRequest { UserId = created.UserId });

        Assert.Equal("Ada", profile.GivenName);
    }

    [Fact]
    public async Task GetUserProfile_ShouldFailWithNotFound_WhenUserIsUnknown()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.GetUserProfileAsync(
            new GetUserProfileRequest { UserId = Guid.NewGuid().ToString() }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task GetUserProfile_ShouldFailWithInvalidArgument_WhenUserIdIsNotGuid()
    {
        using var testScope = host.CreateTestScope();
        var client = host.CreateUserIdentityClient(ServiceId.LaraueBoards);

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.GetUserProfileAsync(
            new GetUserProfileRequest { UserId = "not-a-guid" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    private static async Task<string> CreateGoogleUserAsync(
        UserIdentityService.UserIdentityServiceClient client,
        CreateUserIfNotExistsByGoogleRequest request)
    {
        var response = await client.CreateUserIfNotExistsByGoogleAsync(request);
        return response.UserId;
    }
}
