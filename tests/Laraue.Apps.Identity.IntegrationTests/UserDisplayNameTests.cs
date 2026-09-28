using Laraue.Apps.Identity.Services;

namespace Laraue.Apps.Identity.IntegrationTests;

public class UserDisplayNameTests
{
    [Theory]
    [InlineData("ada", "Ada", "Lovelace", "ada", "AD")]
    [InlineData(null, "Ada", "Lovelace", "Ada Lovelace", "AL")]
    [InlineData(" ", "ada", "lovelace", "ada lovelace", "AL")]
    [InlineData(null, "Ada", null, "Ada", "AD")]
    [InlineData(null, null, "Lovelace", "Lovelace", "LO")]
    [InlineData(null, "A", null, "A", "A")]
    [InlineData(null, null, null, "Unknown", "UN")]
    public void From_ShouldDeriveNameAndInitials_Always(
        string? userName,
        string? givenName,
        string? familyName,
        string displayName,
        string initials)
    {
        var result = UserDisplayName.From(userName, givenName, familyName);

        Assert.Equal(displayName, result.DisplayName);
        Assert.Equal(initials, result.Initials);
    }
}
