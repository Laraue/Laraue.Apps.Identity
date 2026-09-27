namespace Laraue.Apps.Identity.Services;

/// <summary>
/// How a user is shown: a display name and up to two initials, derived from their own profile (see
/// <see cref="DataAccess.Entities.User.UserName"/>) - the user name if there is one, otherwise
/// "Given Family", otherwise whichever of the two is set, otherwise "Unknown".
/// </summary>
public sealed record UserDisplayName(string DisplayName, string Initials)
{
    public static UserDisplayName From(string? userName, string? givenName, string? familyName)
    {
        var (displayName, initials) = (userName, givenName, familyName) switch
        {
            _ when !string.IsNullOrWhiteSpace(userName) => (userName, FirstLetters(userName)),
            _ when !string.IsNullOrWhiteSpace(givenName) && !string.IsNullOrWhiteSpace(familyName)
                => ($"{givenName} {familyName}", $"{givenName[0]}{familyName[0]}"),
            _ when !string.IsNullOrWhiteSpace(givenName) => (givenName, FirstLetters(givenName)),
            _ when !string.IsNullOrWhiteSpace(familyName) => (familyName, FirstLetters(familyName)),
            _ => ("Unknown", "UN"),
        };

        return new UserDisplayName(displayName, initials.ToUpperInvariant());
    }

    private static string FirstLetters(string name) => name.Length > 1 ? name[..2] : name;
}
