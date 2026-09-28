using System.ComponentModel.DataAnnotations;

namespace Laraue.Apps.Identity.DataAccess.Entities;

/// <summary>
/// A global Laraue user identity, shared across every consuming service. The row itself carries no
/// credential - see <see cref="TelegramAccount"/>/<see cref="GoogleAccount"/> for how a user actually
/// authenticates.
/// </summary>
public class User
{
    /// <summary>
    /// Max length of <see cref="GivenName"/>/<see cref="FamilyName"/>. Telegram names (at most 64) always
    /// fit; a longer Google name is cut to it when the user is created.
    /// </summary>
    public const int NameMaxLength = 128;

    // GivenName + " " + FamilyName at their max length, the longest name DisplayName can be derived from.
    public const int DisplayNameMaxLength = 2 * NameMaxLength + 1;

    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The user's own profile - who they are, as opposed to what an account's provider last reported.
    /// Filled once from the first sign-in account the user was created with (Telegram username/first/
    /// last name, Google given/family name) and not overwritten by later account refreshes or links,
    /// so it can be changed by the user independently of their accounts.
    /// </summary>
    // Telegram's username limit - the only source of a user name.
    [MaxLength(32)]
    public string? UserName { get; set; }

    /// <inheritdoc cref="UserName"/>
    [MaxLength(NameMaxLength)]
    public string? GivenName { get; set; }

    /// <inheritdoc cref="UserName"/>
    [MaxLength(NameMaxLength)]
    public string? FamilyName { get; set; }

    /// <summary>
    /// How the user is shown, derived from <see cref="UserName"/>/<see cref="GivenName"/>/
    /// <see cref="FamilyName"/> when they're set (see <c>UserDisplayName</c> in Services) and stored,
    /// so callers show the same name without each deriving it their own way. The user can then change
    /// it (<c>UpdateUserProfile</c>) - stored as given, not derived again.
    /// </summary>
    [MaxLength(DisplayNameMaxLength)]
    public string DisplayName { get; set; } = string.Empty;

    /// <inheritdoc cref="DisplayName"/>
    [MaxLength(2)]
    public string Initials { get; set; } = string.Empty;

    /// <summary>
    /// Set when this user's last sign-in account was moved to another user (account linking,
    /// BRD-218): the user this one was absorbed into. Such a user has no way to sign in any more; the
    /// row is kept as a trail rather than deleted.
    /// </summary>
    public Guid? MergedIntoUserId { get; set; }

    public DateTime? MergedAt { get; set; }

    /// <summary>
    /// The user's Telegram account, if linked. At most one per user - enforced by a unique index on
    /// <see cref="Entities.TelegramAccount.UserId"/>, not only by account linking's own check.
    /// </summary>
    public TelegramAccount? TelegramAccount { get; set; }

    /// <summary>
    /// The user's Google account, if linked. At most one per user, same as <see cref="TelegramAccount"/>.
    /// </summary>
    public GoogleAccount? GoogleAccount { get; set; }
}
