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
    /// Scan *exactly* the assemblies you pass in, register all IRequestHandler<,>,
    /// INotificationHandler<>, and IPipelineBehavior<,> found in those assemblies,
    /// then register the EBus Mediator itself.
    /// </summary>
    /// <param name="services">The IServiceCollection to add to.</param>
    /// <param name="assemblies">
    /// One or more assemblies to scan for handler and behavior implementations.
    /// You must pass at least one assembly (e.g. Assembly.GetExecutingAssembly()).
    /// </param>
    /// <returns>The IServiceCollection, for chaining.</returns>
    public static IServiceCollection AddEBus(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            throw new ArgumentException("You must provide at least one assembly to scan.", nameof(assemblies));

        services.AddTransient<IMediator, Mediator>();

        RegisterRequestHandlers(services, assemblies);
        RegisterNotificationHandlers(services, assemblies);
        RegisterPipelineBehaviors(services, assemblies);

        return services;
    }

    /// <summary>
    /// Scan the entry assembly plus all of its referenced assemblies,
    /// registers all IRequestHandler<,>, INotificationHandler<>, and IPipelineBehavior<,> implementations
    /// found in those assemblies, and then registers the EBus Mediator itself.
    /// </summary>
    public static IServiceCollection AddEBus(this IServiceCollection services)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null)
            throw new InvalidOperationException("Could not determine entry assembly.");

        var toScan = new List<Assembly> { entryAssembly };

        foreach (var asmName in entryAssembly.GetReferencedAssemblies())
        {
            try
            {
                var loaded = Assembly.Load(asmName);
                toScan.Add(loaded);
            }
            catch
            {
                // If it fails to load, ignore (e.g. dynamic or unrelated system assemblies)
            }
        }

        var distinct = toScan
            .Where(a => a != null)
            .Distinct()
            .ToArray();

        return services.AddEBus(distinct);
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

    private static void RegisterPipelineBehaviors(IServiceCollection services, Assembly[] assemblies)
    {
        var openInterfaceType = typeof(IPipelineBehaviour<,>);

        foreach (var assembly in assemblies)
        {
            var implementations = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && !t.IsAbstract)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType
                                && i.GetGenericTypeDefinition() == openInterfaceType)
                    .Select(i => new { Service = i, Implementation = t }));

            foreach (var pair in implementations)
            {
                services.AddTransient(pair.Service, pair.Implementation);
            }
        }
    }

}
