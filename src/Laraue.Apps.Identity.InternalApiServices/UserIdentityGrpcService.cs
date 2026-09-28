using Grpc.Core;
using Laraue.Apps.Identity.Services;
using Laraue.Core.Exceptions.Web;
// The generated proto service is also called `UserIdentityService`, colliding with the business
// logic class of the same name in Laraue.Apps.Identity.Services - alias it to keep both usable here.
using ContractsUserIdentityService = Laraue.Apps.Identity.Internal.Contracts.UserIdentityService;
using ContractsServiceId = Laraue.Apps.Identity.Internal.Contracts.ServiceId;
using DomainServiceId = Laraue.Apps.Identity.DataAccess.Entities.ServiceId;

namespace Laraue.Apps.Identity.InternalApiServices;

/// <summary>
/// gRPC-facing adapter over <see cref="IUserIdentityService"/>: translates between the wire
/// contract (<c>Laraue.Apps.Identity.Internal.Contracts</c>, a proto <c>ServiceId</c> enum, a
/// string user id) and the DB-backed business logic (the domain <c>ServiceId</c> enum, a
/// <see cref="Guid"/> user id). No business logic of its own.
/// </summary>
public sealed class UserIdentityGrpcService(IUserIdentityService userIdentityService)
    : ContractsUserIdentityService.UserIdentityServiceBase
{
    public override async Task<Internal.Contracts.CreateUserIfNotExistsResponse> CreateUserIfNotExists(
        Internal.Contracts.CreateUserIfNotExistsRequest request,
        ServerCallContext context)
    {
        Guid userId;

        try
        {
            userId = await userIdentityService.CreateUserIfNotExistsAsync(
                ReadDomainServiceId(context),
                request.TelegramId,
                ToTelegramProfile(request),
                context.CancellationToken);
        }
        catch (BadRequestException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }

        return new Internal.Contracts.CreateUserIfNotExistsResponse { UserId = userId.ToString() };
    }

    public override async Task<Internal.Contracts.CreateUserIfNotExistsResponse> CreateUserIfNotExistsByGoogle(
        Internal.Contracts.CreateUserIfNotExistsByGoogleRequest request,
        ServerCallContext context)
    {
        Guid userId;

        try
        {
            userId = await userIdentityService.CreateUserIfNotExistsByGoogleAsync(
                ReadDomainServiceId(context),
                request.GoogleSubject,
                ToGoogleProfile(request),
                context.CancellationToken);
        }
        catch (BadRequestException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }

        return new Internal.Contracts.CreateUserIfNotExistsResponse { UserId = userId.ToString() };
    }

    public override async Task<Internal.Contracts.LinkAccountResponse> LinkTelegramAccount(
        Internal.Contracts.LinkTelegramAccountRequest request,
        ServerCallContext context)
    {
        var result = await ExecuteLinkAsync(() => userIdentityService.LinkTelegramAccountAsync(
            ReadDomainServiceId(context),
            ParseUserId(request.UserId),
            request.TelegramId,
            new TelegramProfile(
                UserName: request.HasTelegramUsername ? request.TelegramUsername : null,
                FirstName: request.HasTelegramFirstName ? request.TelegramFirstName : null,
                LastName: request.HasTelegramLastName ? request.TelegramLastName : null,
                LanguageCode: request.HasTelegramLanguageCode ? request.TelegramLanguageCode : null),
            context.CancellationToken));

        return ToLinkAccountResponse(result);
    }

    public override async Task<Internal.Contracts.LinkAccountResponse> LinkGoogleAccount(
        Internal.Contracts.LinkGoogleAccountRequest request,
        ServerCallContext context)
    {
        var result = await ExecuteLinkAsync(() => userIdentityService.LinkGoogleAccountAsync(
            ReadDomainServiceId(context),
            ParseUserId(request.UserId),
            request.GoogleSubject,
            new GoogleProfile(
                Email: request.HasEmail ? request.Email : null,
                Name: request.HasName ? request.Name : null,
                GivenName: request.HasGivenName ? request.GivenName : null,
                FamilyName: request.HasFamilyName ? request.FamilyName : null),
            context.CancellationToken));

        return ToLinkAccountResponse(result);
    }

    public override async Task<Internal.Contracts.GetUserProfileResponse> GetUserProfile(
        Internal.Contracts.GetUserProfileRequest request,
        ServerCallContext context)
    {
        UserProfile profile;

        try
        {
            profile = await userIdentityService.GetUserProfileAsync(ParseUserId(request.UserId), context.CancellationToken);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }

        return ToGetUserProfileResponse(profile);
    }

    public override async Task<Internal.Contracts.GetUserProfileResponse> UpdateUserProfile(
        Internal.Contracts.UpdateUserProfileRequest request,
        ServerCallContext context)
    {
        UserProfile profile;

        try
        {
            profile = await userIdentityService.UpdateUserProfileAsync(
                ParseUserId(request.UserId),
                new UserProfileUpdate(
                    GivenName: request.HasGivenName ? request.GivenName : null,
                    FamilyName: request.HasFamilyName ? request.FamilyName : null,
                    DisplayName: request.DisplayName),
                context.CancellationToken);
        }
        catch (BadRequestException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }

        return ToGetUserProfileResponse(profile);
    }

    private static Internal.Contracts.GetUserProfileResponse ToGetUserProfileResponse(UserProfile profile)
    {
        var response = new Internal.Contracts.GetUserProfileResponse
        {
            DisplayName = profile.DisplayName,
            Initials = profile.Initials,
        };
        if (profile.UserName is not null)
            response.UserName = profile.UserName;
        if (profile.GivenName is not null)
            response.GivenName = profile.GivenName;
        if (profile.FamilyName is not null)
            response.FamilyName = profile.FamilyName;
        if (profile.GoogleEmail is not null)
            response.GoogleEmail = profile.GoogleEmail;

        return response;
    }

    private static async Task<LinkAccountResult> ExecuteLinkAsync(Func<Task<LinkAccountResult>> link)
    {
        try
        {
            return await link();
        }
        catch (BadRequestException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    private static Guid ParseUserId(string userId)
    {
        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid user id '{userId}'."));
    }

    private static Internal.Contracts.LinkAccountResponse ToLinkAccountResponse(LinkAccountResult result)
    {
        var response = new Internal.Contracts.LinkAccountResponse
        {
            Result = result.Outcome switch
            {
                LinkAccountOutcome.Linked => Internal.Contracts.LinkAccountResult.Linked,
                LinkAccountOutcome.Moved => Internal.Contracts.LinkAccountResult.Moved,
                LinkAccountOutcome.OwnerUsedByAnotherService => Internal.Contracts.LinkAccountResult.OwnerUsedByAnotherService,
                LinkAccountOutcome.UserHasOtherAccount => Internal.Contracts.LinkAccountResult.UserHasOtherAccount,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, null),
            },
        };

        if (result.PreviousUserId is { } previousUserId)
        {
            response.PreviousUserId = previousUserId.ToString();
        }

        return response;
    }

    /// <summary>
    /// The calling service is identified once per client via the
    /// <see cref="Internal.Contracts.GrpcHeaders.ServiceIdHeaderName"/> metadata header (attached by
    /// an interceptor on the client, not per-call) rather than a request field - see the note atop
    /// <c>user_identity.proto</c>.
    /// </summary>
    private static DomainServiceId ReadDomainServiceId(ServerCallContext context)
    {
        var header = context.RequestHeaders.Get(Internal.Contracts.GrpcHeaders.ServiceIdHeaderName)?.Value;

        if (header is null || !int.TryParse(header, out var rawServiceId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                $"Missing or invalid '{Internal.Contracts.GrpcHeaders.ServiceIdHeaderName}' header."));
        }

        return ToDomainServiceId((ContractsServiceId)rawServiceId);
    }

    private static DomainServiceId ToDomainServiceId(ContractsServiceId serviceId) => serviceId switch
    {
        ContractsServiceId.LaraueBoards => DomainServiceId.LaraueBoards,
        ContractsServiceId.LearnLanguage => DomainServiceId.LearnLanguage,
        _ => throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unknown service '{serviceId}'.")),
    };

    private static TelegramProfile ToTelegramProfile(Internal.Contracts.CreateUserIfNotExistsRequest request) => new(
        UserName: request.HasTelegramUsername ? request.TelegramUsername : null,
        FirstName: request.HasTelegramFirstName ? request.TelegramFirstName : null,
        LastName: request.HasTelegramLastName ? request.TelegramLastName : null,
        LanguageCode: request.HasTelegramLanguageCode ? request.TelegramLanguageCode : null);

    private static GoogleProfile ToGoogleProfile(Internal.Contracts.CreateUserIfNotExistsByGoogleRequest request) => new(
        Email: request.HasEmail ? request.Email : null,
        Name: request.HasName ? request.Name : null,
        GivenName: request.HasGivenName ? request.GivenName : null,
        FamilyName: request.HasFamilyName ? request.FamilyName : null);
}
