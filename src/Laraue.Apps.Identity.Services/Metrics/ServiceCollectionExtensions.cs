using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Laraue.Apps.Identity.Services.Metrics;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="IdentityMetrics"/>, the event counters. A host also adds
        /// <see cref="IdentityMetrics.MeterName"/> to its OpenTelemetry meters.
        /// </summary>
        public IServiceCollection AddIdentityMetrics()
        {
            services.AddMetrics();
            services.TryAddSingleton<IdentityMetrics>();

            return services;
        }
    }
}
