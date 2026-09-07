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

    public User User { get; set; } = null!;

    public ServiceId ServiceId { get; set; }

    public Service Service { get; set; } = null!;

    public DateTimeOffset FirstSeenAt { get; set; }
}
