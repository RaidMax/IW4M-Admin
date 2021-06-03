using Integrations.Source.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Integrations.Source.Extensions
{
    public static class IntegrationServicesExtensions
    {
        public static IServiceCollection AddSource(this IServiceCollection services)
        {
            services.AddSingleton<IRConClientFactory, RConClientFactory>();

            return services;
        }
    }
}