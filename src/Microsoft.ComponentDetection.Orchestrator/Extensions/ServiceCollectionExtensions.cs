namespace Microsoft.ComponentDetection.Orchestrator.Extensions;

using Microsoft.Extensions.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Component Detection services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to register the services with.</param>
    /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
    public static IServiceCollection AddComponentDetection(this IServiceCollection services) => services;
}
