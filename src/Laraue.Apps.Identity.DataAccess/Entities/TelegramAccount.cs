namespace Laraue.Apps.Identity.DataAccess.Entities;

/// <summary>
/// Links a Telegram account to a global <see cref="User"/>. <see cref="TelegramId"/> is the lookup
/// key - it's unique per Telegram account, not per calling service, so the same Telegram account
/// always resolves to the same <see cref="UserId"/> regardless of which service is asking.
/// </summary>
public class TelegramAccount
{
    public long TelegramId { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
