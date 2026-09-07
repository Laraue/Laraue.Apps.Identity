namespace Laraue.Apps.Identity.DataAccess.Entities;

/// <summary>
/// A global Laraue user identity, shared across every consuming service. The row itself carries no
/// credential - see <see cref="TelegramAccount"/> (and, in a later stage, a Google account
/// equivalent) for how a user actually authenticates.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
