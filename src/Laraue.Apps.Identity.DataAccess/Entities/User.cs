namespace Laraue.Apps.Identity.DataAccess.Entities;

/// <summary>
/// A global Laraue user identity, shared across every consuming service. The row itself carries no
/// credential - see <see cref="TelegramAccount"/> (and, in a later stage, a Google account
/// equivalent) for how a user actually authenticates.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Set when this user's last sign-in account was moved to another user (account linking,
    /// BRD-218): the user this one was absorbed into. Such a user has no way to sign in any more; the
    /// row is kept as a trail rather than deleted.
    /// </summary>
    public Guid? MergedIntoUserId { get; set; }

    public DateTime? MergedAt { get; set; }
}
