using EBus.Abstractions;
using EBus.Implementation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace EBus.Registration;

/// <summary>
/// Extension methods to register EBus (the Mediator + all handlers and behaviors) in IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scans the given assemblies, registers all IRequestHandler<,>, INotificationHandler<>, 
    /// and IPipelineBehavior<,> implementations, and registers the EBus Mediator itself.
    /// </summary>
    /// <param name="services">The IServiceCollection to add to.</param>
    /// <param name="assemblies">
    /// One or more assemblies to scan for handler and behavior implementations.
    /// You must pass at least one assembly (e.g. Assembly.GetExecutingAssembly()).
    /// </param>
    /// <returns>The IServiceCollection, for chaining.</returns>
    public static IServiceCollection AddEBus(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0) throw new ArgumentException("You must provide at least one assembly to scan.", nameof(assemblies));

        services.AddTransient<IMediator, Mediator>();

        RegisterRequestHandlers(services, assemblies);
        RegisterNotificationHandlers(services, assemblies);
        RegisterPipelineBehaviours(services, assemblies);

        return services;
    }

    private static void RegisterRequestHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var openInterfaceType = typeof(IRequestHandler<,>);

        foreach (var assembly in assemblies)
        {
            var types = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && !t.IsAbstract)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType &&
                                i.GetGenericTypeDefinition() == openInterfaceType)
                    .Select(i => new { Implementation = t, Service = i})
                );
            foreach (var pair in types)
            {
                services.AddTransient(pair.Service, pair.Implementation);
            }
        }
    }

    private static void RegisterNotificationHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var openInterfaceType = typeof(INotificationHandler<>);

        foreach (var assembly in assemblies)
        {
            var types = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && !t.IsAbstract)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == openInterfaceType)
                    .Select(i => new { Implementation = t, Service = i })
                );
            foreach (var pair in types)
            {
                services.AddTransient(pair.Service, pair.Implementation);
            }
        }
    }

    private static void RegisterPipelineBehaviours(IServiceCollection services, Assembly[] assemblies)
    {
        var openInterfaceType = typeof(IPipelineBehaviour<,>);

        foreach (var assembbly in assemblies)
        {
            var types = assembbly
                .GetTypes()
                .Where(t => !t.IsInterface && !t.IsAbstract)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == openInterfaceType)
                    .Select(i => new { Implementation = t, Service = i })
                );
            foreach (var pair in types)
            {
                services.AddTransient(pair.Service, pair.Implementation);
            }
        }
    }

}
