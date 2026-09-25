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

/// <summary>
/// Google profile claims as currently known by the calling service - see <see cref="GoogleAccount"/>.
/// All optional: which claims a Google ID token carries depends on the scopes the caller requested.
/// </summary>
public sealed record GoogleProfile(
    string? Email,
    string? Name,
    string? GivenName,
    string? FamilyName);

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

    /// <summary>
    /// Google counterpart of <see cref="CreateUserIfNotExistsAsync"/>: resolves the global user id
    /// for the given Google account (its ID token's <c>sub</c> claim), creating a new global user
    /// and linking the Google account to it if none exists yet. Records <paramref name="serviceId"/>
    /// as used and refreshes the stored profile the same way. The caller must have verified the ID
    /// token already - this method trusts <paramref name="googleSubject"/> as given.
    /// </summary>
    Task<Guid> CreateUserIfNotExistsByGoogleAsync(
        ServiceId serviceId,
        string googleSubject,
        GoogleProfile profile,
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
        EnsureServiceIsKnown(serviceId);

        var userId = await GetOrCreateUserIdAsync(telegramId, profile, cancellationToken);

        await EnsureUserServiceRecordedAsync(userId, serviceId, cancellationToken);

        return userId;
    }

    public async Task<Guid> CreateUserIfNotExistsByGoogleAsync(
        ServiceId serviceId,
        string googleSubject,
        GoogleProfile profile,
        CancellationToken cancellationToken)
    {
        EnsureServiceIsKnown(serviceId);

        if (string.IsNullOrWhiteSpace(googleSubject))
        {
            throw new BadRequestException(nameof(googleSubject), Errors.GoogleSubjectRequired);
        }

        var userId = await GetOrCreateUserIdByGoogleAsync(googleSubject, profile, cancellationToken);

        await EnsureUserServiceRecordedAsync(userId, serviceId, cancellationToken);

        return userId;
    }

    private static void EnsureServiceIsKnown(ServiceId serviceId)
    {
        if (!Enum.IsDefined(serviceId))
        {
            throw new BadRequestException(nameof(serviceId), string.Format(Errors.UnknownService, serviceId));
        }
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

    /// <summary>
    /// Google counterpart of <see cref="GetOrCreateUserIdAsync"/>, including its lost-race
    /// handling for two concurrent first logins with the same Google account.
    /// </summary>
    private async Task<Guid> GetOrCreateUserIdByGoogleAsync(
        string googleSubject,
        GoogleProfile profile,
        CancellationToken cancellationToken)
    {
        var existingAccount = await context.GoogleAccounts
            .SingleOrDefaultAsync(x => x.GoogleSubject == googleSubject, cancellationToken);

        if (existingAccount is not null)
        {
            ApplyProfile(existingAccount, profile);
            await context.SaveChangesAsync(cancellationToken);

            return existingAccount.UserId;
        }

        var newUser = new User { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        var newAccount = new GoogleAccount
        {
            GoogleSubject = googleSubject,
            UserId = newUser.Id,
            CreatedAt = newUser.CreatedAt.UtcDateTime,
        };
        ApplyProfile(newAccount, profile);

        context.Users.Add(newUser);
        context.GoogleAccounts.Add(newAccount);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return newUser.Id;
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent call for the same Google account - the GoogleSubject
            // primary key rejected our insert. Drop our attempt and use whichever row won.
            context.Entry(newUser).State = EntityState.Detached;
            context.Entry(newAccount).State = EntityState.Detached;

            return await context.GoogleAccounts
                .Where(x => x.GoogleSubject == googleSubject)
                .Select(x => x.UserId)
                .SingleAsync(cancellationToken);
        }
    }

    private static void ApplyProfile(GoogleAccount account, GoogleProfile profile)
    {
        account.Email = profile.Email;
        account.Name = profile.Name;
        account.GivenName = profile.GivenName;
        account.FamilyName = profile.FamilyName;
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
