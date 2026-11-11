namespace Laraue.Apps.Identity.DataAccess.Entities;

/// <summary>
/// Records that a global <see cref="User"/> has used a given <see cref="Service"/> - the basis for
/// "which services does this user already use" (composite key <see cref="UserId"/>/
/// <see cref="ServiceId"/>, one row per user per service regardless of how many times
/// CreateUserIfNotExists is called for that pair).
/// </summary>
public class UserService
{
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public ServiceId ServiceId { get; set; }

    public Service? Service { get; set; }

    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// Where the user came to this service from (BRD-295) - a landing link's ref/utm_source, a bot /start parameter,
    /// a Mini App start_param - as the calling service sent it with the call that created this row. Never changed
    /// afterwards; null when the caller didn't know it, and for rows from before BRD-295.
    /// </summary>
    [System.ComponentModel.DataAnnotations.MaxLength(SourceMaxLength)]
    public string? Source { get; set; }

    public const int SourceMaxLength = 64;
}
