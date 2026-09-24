using System.ComponentModel.DataAnnotations;

namespace Laraue.Apps.Identity.DataAccess.Entities;

/// <summary>
/// Links a Google account to a global <see cref="User"/> - the Google counterpart of
/// <see cref="TelegramAccount"/>. <see cref="GoogleSubject"/> (the ID token's <c>sub</c> claim) is
/// the lookup key: it's Google's stable, never-reused account id, unlike <see cref="Email"/>, which
/// the account owner can change. The profile fields mirror the ID token's standard claims
/// (<c>email</c>, <c>name</c>, <c>given_name</c>, <c>family_name</c>) and, same as
/// <see cref="TelegramAccount"/>'s, are refreshed on every call rather than only captured once.
/// </summary>
public class GoogleAccount
{
    /// <summary>
    /// Google's <c>sub</c> claim - an ASCII string of at most 255 characters per the OpenID Connect
    /// spec (in practice a ~21-digit number, but Google doesn't promise that shape).
    /// </summary>
    [MaxLength(255)]
    public required string GoogleSubject { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    [MaxLength(320)]
    public string? Email { get; set; }

    [MaxLength(255)]
    public string? Name { get; set; }

    [MaxLength(255)]
    public string? GivenName { get; set; }

    [MaxLength(255)]
    public string? FamilyName { get; set; }

    public DateTime CreatedAt { get; set; }
}
