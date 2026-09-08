using Laraue.Apps.Identity.DataAccess;
using Laraue.Apps.Identity.DataAccess.Entities;
using Laraue.Apps.Identity.Services.Resources;
using Laraue.Core.Exceptions.Web;
using Microsoft.EntityFrameworkCore;
// UserIdentityService.cs (this business-logic class) shares its name with the DataAccess entity
// UserService (user-uses-service link) - alias the entity to keep both unambiguous.
using UserServiceEntity = Laraue.Apps.Identity.DataAccess.Entities.UserService;

namespace Laraue.Apps.Identity.Services;

/// <summary>
/// Telegram profile fields as currently known by the calling service - see
/// <see cref="TelegramAccount"/> for why these mirror <c>ITelegramUser</c>'s shape. All optional:
/// not every Telegram user has a username, and a caller may not always have the full profile on
/// hand.
/// </summary>
public sealed record TelegramProfile(
    string? UserName,
    string? FirstName,
    string? LastName,
    string? LanguageCode);

public interface IUserIdentityService
{
    /// <summary>
    /// Resolves the global user id for the given Telegram account, creating a new global user and
    /// linking the Telegram account to it if none exists yet. Either way, records that
    /// <paramref name="serviceId"/> is used by the resolved user, and refreshes the stored Telegram
    /// profile fields from <paramref name="profile"/> (Telegram profiles can change between calls).
    /// </summary>
    Task<Guid> CreateUserIfNotExistsAsync(
        ServiceId serviceId,
        long telegramId,
        TelegramProfile profile,
        CancellationToken cancellationToken);
}

public class UserIdentityService(DatabaseContext context) : IUserIdentityService
{
    public async Task<Guid> CreateUserIfNotExistsAsync(
        ServiceId serviceId,
        long telegramId,
        TelegramProfile profile,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(serviceId))
        {
            throw new BadRequestException(nameof(serviceId), string.Format(Errors.UnknownService, serviceId));
        }

        var userId = await GetOrCreateUserIdAsync(telegramId, profile, cancellationToken);

        await EnsureUserServiceRecordedAsync(userId, serviceId, cancellationToken);

        return userId;
    }

    /// <summary>
    /// Looks up the user already linked to <paramref name="telegramId"/>, or creates a new global
    /// user and links it if this is the first time this Telegram account has been seen. Either way,
    /// writes <paramref name="profile"/> onto the <see cref="TelegramAccount"/> row.
    /// </summary>
    private async Task<Guid> GetOrCreateUserIdAsync(
        long telegramId,
        TelegramProfile profile,
        CancellationToken cancellationToken)
    {
        var existingAccount = await context.TelegramAccounts
            .SingleOrDefaultAsync(x => x.TelegramId == telegramId, cancellationToken);

        if (existingAccount is not null)
        {
            ApplyProfile(existingAccount, profile);
            await context.SaveChangesAsync(cancellationToken);

            return existingAccount.UserId;
        }

        var newUser = new User { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        var newAccount = new TelegramAccount
        {
            TelegramId = telegramId,
            UserId = newUser.Id,
            CreatedAt = newUser.CreatedAt,
        };
        ApplyProfile(newAccount, profile);

        context.Users.Add(newUser);
        context.TelegramAccounts.Add(newAccount);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return newUser.Id;
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent call for the same Telegram account - the unique
            // TelegramId key rejected our insert. Drop our attempt and use whichever row won.
            context.Entry(newUser).State = EntityState.Detached;
            context.Entry(newAccount).State = EntityState.Detached;

            return await context.TelegramAccounts
                .Where(x => x.TelegramId == telegramId)
                .Select(x => x.UserId)
                .SingleAsync(cancellationToken);
        }
    }

    private static void ApplyProfile(TelegramAccount account, TelegramProfile profile)
    {
        account.TelegramUserName = profile.UserName;
        account.TelegramFirstName = profile.FirstName;
        account.TelegramLastName = profile.LastName;
        account.TelegramLanguageCode = profile.LanguageCode;
    }

    private async Task EnsureUserServiceRecordedAsync(
        Guid userId,
        ServiceId serviceId,
        CancellationToken cancellationToken)
    {
        var alreadyRecorded = await context.UserServices
            .AnyAsync(x => x.UserId == userId && x.ServiceId == serviceId, cancellationToken);

        if (alreadyRecorded)
        {
            return;
        }

        context.UserServices.Add(new UserServiceEntity
        {
            UserId = userId,
            ServiceId = serviceId,
            FirstSeenAt = DateTimeOffset.UtcNow,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent call recording the same (UserId, ServiceId) pair -
            // it's already recorded, which is all this method promises.
        }
    }
}
