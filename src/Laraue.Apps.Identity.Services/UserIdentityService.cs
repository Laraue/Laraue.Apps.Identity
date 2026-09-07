using Laraue.Apps.Identity.DataAccess;
using Laraue.Apps.Identity.DataAccess.Entities;
using Laraue.Apps.Identity.Services.Resources;
using Laraue.Core.Exceptions.Web;
using Microsoft.EntityFrameworkCore;
// UserIdentityService.cs (this business-logic class) shares its name with the DataAccess entity
// UserService (user-uses-service link) - alias the entity to keep both unambiguous.
using UserServiceEntity = Laraue.Apps.Identity.DataAccess.Entities.UserService;

namespace Laraue.Apps.Identity.Services;

public interface IUserIdentityService
{
    /// <summary>
    /// Resolves the global user id for the given Telegram account, creating a new global user and
    /// linking the Telegram account to it if none exists yet. Either way, records that
    /// <paramref name="serviceId"/> is used by the resolved user.
    /// </summary>
    Task<Guid> CreateUserIfNotExistsAsync(
        ServiceId serviceId,
        long telegramId,
        CancellationToken cancellationToken);
}

public class UserIdentityService(DatabaseContext context) : IUserIdentityService
{
    public async Task<Guid> CreateUserIfNotExistsAsync(
        ServiceId serviceId,
        long telegramId,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(serviceId))
        {
            throw new BadRequestException(nameof(serviceId), string.Format(Errors.UnknownService, serviceId));
        }

        var userId = await GetOrCreateUserIdAsync(telegramId, cancellationToken);

        await EnsureUserServiceRecordedAsync(userId, serviceId, cancellationToken);

        return userId;
    }

    /// <summary>
    /// Looks up the user already linked to <paramref name="telegramId"/>, or creates a new global
    /// user and links it if this is the first time this Telegram account has been seen.
    /// </summary>
    private async Task<Guid> GetOrCreateUserIdAsync(long telegramId, CancellationToken cancellationToken)
    {
        var existingUserId = await context.TelegramAccounts
            .Where(x => x.TelegramId == telegramId)
            .Select(x => (Guid?)x.UserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingUserId is { } userId)
        {
            return userId;
        }

        var newUser = new User { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };

        context.Users.Add(newUser);
        context.TelegramAccounts.Add(new TelegramAccount
        {
            TelegramId = telegramId,
            UserId = newUser.Id,
            CreatedAt = newUser.CreatedAt,
        });

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

            return await context.TelegramAccounts
                .Where(x => x.TelegramId == telegramId)
                .Select(x => x.UserId)
                .SingleAsync(cancellationToken);
        }
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
