using Laraue.Apps.Identity.Services;
using Laraue.Apps.Identity.Services.Metrics;
using Laraue.Core.DateTime.Services.Abstractions;
using Laraue.Core.DateTime.Services.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace Laraue.Apps.Identity.InternalApiServices;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInternalApiServices()
        {
            services
                .AddSingleton<IDateTimeProvider, DateTimeProvider>()
                .AddScoped<IUserIdentityService, UserIdentityService>();

            services.AddIdentityMetrics();

            // The database-backed gauges (total users, by sign-in source, by service), refreshed in the background.
            services.AddSingleton<IdentityStateMetrics>();
            services.AddHostedService(sp => sp.GetRequiredService<IdentityStateMetrics>());

            return services;
        }
    }
}
