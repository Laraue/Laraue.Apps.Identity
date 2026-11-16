namespace Laraue.Apps.Identity.Services;

/// <summary>
/// How a user is shown: a display name and up to two initials, derived from their own profile (see
/// <see cref="DataAccess.Entities.User.UserName"/>) - the user name if there is one, otherwise
/// "Given Family", otherwise whichever of the two is set, otherwise "Unknown".
/// </summary>
/// <remarks>
/// Initials are taken by code point (<see cref="System.Text.Rune"/>), not by <c>char</c>: an emoji is two
/// UTF-16 chars, and taking only the first left a lone surrogate the database couldn't store (MIN-57).
/// Postgres counts <c>varchar</c> length in code points, so two emoji still fit <c>Initials</c>.
/// </remarks>
public sealed record UserDisplayName(string DisplayName, string Initials)
{
    public static UserDisplayName From(string? userName, string? givenName, string? familyName)
    {
        var (displayName, initials) = (userName, givenName, familyName) switch
        {
            _ when !string.IsNullOrWhiteSpace(userName) => (userName, FirstLetters(userName, 2)),
            _ when !string.IsNullOrWhiteSpace(givenName) && !string.IsNullOrWhiteSpace(familyName)
                => ($"{givenName} {familyName}", FirstLetters(givenName, 1) + FirstLetters(familyName, 1)),
            _ when !string.IsNullOrWhiteSpace(givenName) => (givenName, FirstLetters(givenName, 2)),
            _ when !string.IsNullOrWhiteSpace(familyName) => (familyName, FirstLetters(familyName, 2)),
            _ => ("Unknown", "UN"),
        };

        return new UserDisplayName(displayName, initials.ToUpperInvariant());
    }

    /// <summary>
    /// Initials of a display name the user set themselves: the first letters of its first two words,
    /// or the first two letters of a single word, upper-cased.
    /// </summary>
    public static string InitialsOf(string displayName)
    {
        var words = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = words.Length > 1
            ? FirstLetters(words[0], 1) + FirstLetters(words[1], 1)
            : FirstLetters(words[0], 2);

        return initials.ToUpperInvariant();
    }

    private static string FirstLetters(string name, int count)
    {
        return string.Concat(name.EnumerateRunes().Take(count));
    }
}
