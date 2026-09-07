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
                ToDomainServiceId(request.ServiceId),
                request.TelegramId,
                context.CancellationToken);
        }
        catch (BadRequestException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }

        return new Internal.Contracts.CreateUserIfNotExistsResponse { UserId = userId.ToString() };
    }

    private static DomainServiceId ToDomainServiceId(ContractsServiceId serviceId) => serviceId switch
    {
        ContractsServiceId.LaraueBoards => DomainServiceId.LaraueBoards,
        ContractsServiceId.LearnLanguage => DomainServiceId.LearnLanguage,
        _ => throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unknown service '{serviceId}'.")),
    };
}
