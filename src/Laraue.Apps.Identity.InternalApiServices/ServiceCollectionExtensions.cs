using Laraue.Apps.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Laraue.Apps.Identity.InternalApiServices;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInternalApiServices()
        {
            services.AddScoped<IUserIdentityService, UserIdentityService>();

            return services;
        }
    }
}
