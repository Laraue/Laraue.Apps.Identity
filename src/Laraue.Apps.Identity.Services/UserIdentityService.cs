using Laraue.Apps.Identity.DataAccess;
using Laraue.Apps.Identity.DataAccess.Entities;
using Laraue.Apps.Identity.Services.Resources;
using Laraue.Core.DateTime.Services.Abstractions;
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

public enum LinkAccountOutcome
{
    /// <summary>Linked to the user - newly, or it already was.</summary>
    Linked,

    /// <summary>Moved to the user from its previous owner, <see cref="LinkAccountResult.PreviousUserId"/>.</summary>
    Moved,

    /// <summary>
    /// Not moved: its current owner, <see cref="LinkAccountResult.PreviousUserId"/>, is also used by
    /// another service, which would lose that user's account.
    /// </summary>
    OwnerUsedByAnotherService,

    /// <summary>The user already has a different account of this kind.</summary>
    UserHasOtherAccount,
}

/// <param name="PreviousUserId">
/// The account's previous owner, for <see cref="LinkAccountOutcome.Moved"/> and
/// <see cref="LinkAccountOutcome.OwnerUsedByAnotherService"/>.
/// </param>
public sealed record LinkAccountResult(LinkAccountOutcome Outcome, Guid? PreviousUserId = null);

/// <summary>
/// A global user's own profile - see <see cref="User.UserName"/>.
/// </summary>
public sealed record UserProfile(
    string? UserName,
    string? GivenName,
    string? FamilyName,
    string DisplayName,
    string Initials);

/// <summary>
/// The parts of a global user's own profile the user can change - see
/// <see cref="IUserIdentityService.UpdateUserProfileAsync"/>.
/// </summary>
public sealed record UserProfileUpdate(
    string? GivenName,
    string? FamilyName,
    string DisplayName);

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

    /// <summary>
    /// Links a Telegram account to the existing global user <paramref name="userId"/> - at most one
    /// per user. If the account belongs to another global user (its current owner), it's moved to
    /// <paramref name="userId"/> unless a service other than <paramref name="serviceId"/> uses that
    /// owner. The caller must check its own copy of the owner has no data before calling. Records <paramref name="serviceId"/> as used by the user and
    /// refreshes the stored profile on success. Throws <see cref="NotFoundException"/> for an unknown
    /// user. The caller must have verified the Telegram login data already.
    /// </summary>
    Task<LinkAccountResult> LinkTelegramAccountAsync(
        ServiceId serviceId,
        Guid userId,
        long telegramId,
        TelegramProfile profile,
        CancellationToken cancellationToken);

    /// <summary>
    /// Google counterpart of <see cref="LinkTelegramAccountAsync"/>, keyed by the verified ID token's
    /// <c>sub</c> claim.
    /// </summary>
    Task<LinkAccountResult> LinkGoogleAccountAsync(
        ServiceId serviceId,
        Guid userId,
        string googleSubject,
        GoogleProfile profile,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns <paramref name="userId"/>'s own profile (see <see cref="User.UserName"/>). Throws
    /// <see cref="NotFoundException"/> for an unknown user.
    /// </summary>
    Task<UserProfile> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces <paramref name="userId"/>'s given/family name and display name with
    /// <paramref name="update"/>'s, stored as given (trimmed, blank names cleared) - the display name
    /// isn't derived from the names again; the initials are derived from it
    /// (<see cref="UserDisplayName.InitialsOf"/>). Returns the updated
    /// profile. Throws <see cref="BadRequestException"/> for an invalid value and
    /// <see cref="NotFoundException"/> for an unknown user.
    /// </summary>
    Task<UserProfile> UpdateUserProfileAsync(
        Guid userId,
        UserProfileUpdate update,
        CancellationToken cancellationToken);
}

public class UserIdentityService(DatabaseContext context, IDateTimeProvider dateTimeProvider) : IUserIdentityService
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

    public async Task<LinkAccountResult> LinkTelegramAccountAsync(
        ServiceId serviceId,
        Guid userId,
        long telegramId,
        TelegramProfile profile,
        CancellationToken cancellationToken)
    {
        EnsureServiceIsKnown(serviceId);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var account = await context.TelegramAccounts
            .SingleOrDefaultAsync(x => x.TelegramId == telegramId, cancellationToken);

        if (account?.UserId != userId
            && await context.TelegramAccounts.AnyAsync(x => x.UserId == userId, cancellationToken))
        {
            return new LinkAccountResult(LinkAccountOutcome.UserHasOtherAccount);
        }

        var previousUserId = account is not null && account.UserId != userId ? account.UserId : (Guid?)null;
        if (previousUserId is not null
            && !await IsUsedOnlyByAsync(previousUserId.Value, serviceId, cancellationToken))
        {
            return new LinkAccountResult(LinkAccountOutcome.OwnerUsedByAnotherService, previousUserId);
        }

        if (account is null)
        {
            account = new TelegramAccount
            {
                TelegramId = telegramId,
                CreatedAt = dateTimeProvider.UtcNow,
            };
            context.TelegramAccounts.Add(account);
        }

        account.UserId = userId;
        ApplyProfile(account, profile);
        if (previousUserId is not null)
        {
            await MarkMergedIfNoAccountsLeftAsync(previousUserId.Value, userId, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        await EnsureUserServiceRecordedAsync(userId, serviceId, cancellationToken);

        return previousUserId is null
            ? new LinkAccountResult(LinkAccountOutcome.Linked)
            : new LinkAccountResult(LinkAccountOutcome.Moved, previousUserId);
    }

    public async Task<UserProfile> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.Users
            .Where(x => x.Id == userId)
            .Select(x => new UserProfile(x.UserName, x.GivenName, x.FamilyName, x.DisplayName, x.Initials))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(string.Format(Errors.UserNotFound, userId));
    }

    public async Task<UserProfile> UpdateUserProfileAsync(
        Guid userId,
        UserProfileUpdate update,
        CancellationToken cancellationToken)
    {
        var givenName = NormalizeOptional(update.GivenName, nameof(update.GivenName), User.NameMaxLength);
        var familyName = NormalizeOptional(update.FamilyName, nameof(update.FamilyName), User.NameMaxLength);
        var displayName = NormalizeRequired(update.DisplayName, nameof(update.DisplayName), User.DisplayNameMaxLength);

        var user = await context.Users
            .Where(x => x.Id == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(string.Format(Errors.UserNotFound, userId));

        user.GivenName = givenName;
        user.FamilyName = familyName;
        user.DisplayName = displayName;
        user.Initials = UserDisplayName.InitialsOf(displayName);
        await context.SaveChangesAsync(cancellationToken);

        return new UserProfile(user.UserName, user.GivenName, user.FamilyName, user.DisplayName, user.Initials);
    }

    private static string? NormalizeOptional(string? value, string fieldName, int maxLength)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length <= maxLength
            ? value
            : throw new BadRequestException(fieldName, string.Format(Errors.ValueTooLong, fieldName, maxLength));
    }

    private static string NormalizeRequired(string? value, string fieldName, int maxLength)
    {
        return NormalizeOptional(value, fieldName, maxLength)
            ?? throw new BadRequestException(fieldName, string.Format(Errors.ValueRequired, fieldName));
    }

    public async Task<LinkAccountResult> LinkGoogleAccountAsync(
        ServiceId serviceId,
        Guid userId,
        string googleSubject,
        GoogleProfile profile,
        CancellationToken cancellationToken)
    {
        EnsureServiceIsKnown(serviceId);

        if (string.IsNullOrWhiteSpace(googleSubject))
        {
            throw new BadRequestException(nameof(googleSubject), Errors.GoogleSubjectRequired);
        }

        await EnsureUserExistsAsync(userId, cancellationToken);

        var account = await context.GoogleAccounts
            .SingleOrDefaultAsync(x => x.GoogleSubject == googleSubject, cancellationToken);

        if (account?.UserId != userId
            && await context.GoogleAccounts.AnyAsync(x => x.UserId == userId, cancellationToken))
        {
            return new LinkAccountResult(LinkAccountOutcome.UserHasOtherAccount);
        }

        var previousUserId = account is not null && account.UserId != userId ? account.UserId : (Guid?)null;
        if (previousUserId is not null
            && !await IsUsedOnlyByAsync(previousUserId.Value, serviceId, cancellationToken))
        {
            return new LinkAccountResult(LinkAccountOutcome.OwnerUsedByAnotherService, previousUserId);
        }

        if (account is null)
        {
            account = new GoogleAccount
            {
                GoogleSubject = googleSubject,
                CreatedAt = dateTimeProvider.UtcNow,
            };
            context.GoogleAccounts.Add(account);
        }

        account.UserId = userId;
        ApplyProfile(account, profile);
        if (previousUserId is not null)
        {
            await MarkMergedIfNoAccountsLeftAsync(previousUserId.Value, userId, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        await EnsureUserServiceRecordedAsync(userId, serviceId, cancellationToken);

        return previousUserId is null
            ? new LinkAccountResult(LinkAccountOutcome.Linked)
            : new LinkAccountResult(LinkAccountOutcome.Moved, previousUserId);
    }

    /// <summary>
    /// Called when an account is being moved away from <paramref name="previousUserId"/> (not saved
    /// yet): if that was the user's last sign-in account, marks them as merged into
    /// <paramref name="userId"/>. A user who still has their other account stays a regular user.
    /// </summary>
    private async Task MarkMergedIfNoAccountsLeftAsync(Guid previousUserId, Guid userId, CancellationToken cancellationToken)
    {
        var accountsLeft = await context.TelegramAccounts.CountAsync(x => x.UserId == previousUserId, cancellationToken)
            + await context.GoogleAccounts.CountAsync(x => x.UserId == previousUserId, cancellationToken);

        if (accountsLeft > 1)
        {
            return;
        }

        var previousUser = await context.Users.SingleAsync(x => x.Id == previousUserId, cancellationToken);
        previousUser.MergedIntoUserId = userId;
        previousUser.MergedAt = dateTimeProvider.UtcNow;
    }

    private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(x => x.Id == userId, cancellationToken))
        {
            throw new NotFoundException(string.Format(Errors.UserNotFound, userId));
        }
    }

    /// <summary>
    /// Whether <paramref name="userId"/> is used by no service other than <paramref name="serviceId"/> -
    /// the condition for moving one of that user's accounts to another user on the service's request.
    /// </summary>
    private Task<bool> IsUsedOnlyByAsync(Guid userId, ServiceId serviceId, CancellationToken cancellationToken)
    {
        return context.UserServices
            .AllAsync(x => x.UserId != userId || x.ServiceId == serviceId, cancellationToken);
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

        var newUser = CreateUser(profile.UserName, profile.FirstName, profile.LastName);
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

    /// <summary>
    /// A new user with their own profile filled from the account they're created with - see
    /// <see cref="User.UserName"/>. Names longer than <see cref="User.NameMaxLength"/> are cut to it.
    /// </summary>
    private User CreateUser(string? userName, string? givenName, string? familyName)
    {
        givenName = TruncateName(givenName);
        familyName = TruncateName(familyName);
        var displayName = UserDisplayName.From(userName, givenName, familyName);

        return new User
        {
            Id = Guid.NewGuid(),
            CreatedAt = dateTimeProvider.UtcNow,
            UserName = userName,
            GivenName = givenName,
            FamilyName = familyName,
            DisplayName = displayName.DisplayName,
            Initials = displayName.Initials,
        };
    }

    private static string? TruncateName(string? name)
    {
        return name?.Length > User.NameMaxLength ? name[..User.NameMaxLength] : name;
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

        var hasSplitName = profile.GivenName is not null || profile.FamilyName is not null;
        var newUser = CreateUser(
            userName: null,
            givenName: hasSplitName ? profile.GivenName : profile.Name ?? profile.Email?.Split('@')[0],
            familyName: hasSplitName ? profile.FamilyName : null);
        var newAccount = new GoogleAccount
        {
            GoogleSubject = googleSubject,
            UserId = newUser.Id,
            CreatedAt = newUser.CreatedAt,
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
            FirstSeenAt = dateTimeProvider.UtcNow,
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
