using System.ComponentModel.DataAnnotations;

namespace Laraue.Apps.Identity.DataAccess.Entities;

/// <summary>
/// Links a Telegram account to a global <see cref="User"/>. <see cref="TelegramId"/> is the lookup
/// key - it's unique per Telegram account, not per calling service, so the same Telegram account
/// always resolves to the same <see cref="UserId"/> regardless of which service is asking. The
/// profile fields (<see cref="TelegramUserName"/> etc.) mirror
/// <c>Laraue.Telegram.NET.Authentication.Models.ITelegramUser</c>'s shape (same names/lengths) for
/// consistency with how the other Laraue apps model a Telegram user, even though this entity
/// doesn't implement that interface itself - keep them in sync if that interface changes.
/// </summary>
public class TelegramAccount
{
    public long TelegramId { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    [MaxLength(32)]
    public string? TelegramUserName { get; set; }

    [MaxLength(64)]
    public string? TelegramFirstName { get; set; }

    [MaxLength(64)]
    public string? TelegramLastName { get; set; }

    [MaxLength(2)]
    public string? TelegramLanguageCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
