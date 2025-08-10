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
        var entry = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Could not determine entry assembly.");

        var toScan = GetTransitiveAssemblies(entry);
        
        var loaded = AppDomain.CurrentDomain.GetAssemblies();
        var all = toScan.Concat(loaded).DistinctBy(a => a.FullName).ToArray();

        return services.AddEBus(all);
    }

    private static IEnumerable<Assembly> GetTransitiveAssemblies(Assembly root)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var q = new Queue<Assembly>();
        void Enqueue(Assembly a)
        {
            if (a?.FullName is null) return;
            if (seen.Add(a.FullName)) q.Enqueue(a);
        }

        Enqueue(root);

        while (q.Count > 0)
        {
            var asm = q.Dequeue();
            yield return asm;

            foreach (var an in asm.GetReferencedAssemblies())
            {
                if (IsSystemAssembly(an)) continue;

                try
                {
                    var dep = Assembly.Load(an);
                    Enqueue(dep);
                }
                catch
                {
                    // ignore load failures for irrelevant/dynamic assemblies
                }
            }
        }
    }

    private static bool IsSystemAssembly(AssemblyName an)
    {
        var n = an.Name ?? string.Empty;
        return n.StartsWith("System", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
            || n is "mscorlib" or "netstandard" or "WindowsBase" or "PresentationCore" or "PresentationFramework";
    }

    private static void RegisterRequestHandlers(IServiceCollection services, Assembly[] assemblies)
        => RegisterOpenGeneric(services, assemblies, typeof(IRequestHandler<,>));

    private static void RegisterNotificationHandlers(IServiceCollection services, Assembly[] assemblies)
        => RegisterOpenGeneric(services, assemblies, typeof(INotificationHandler<>));

    private static void RegisterPipelineBehaviors(IServiceCollection services, Assembly[] assemblies)
        => RegisterOpenGeneric(services, assemblies, typeof(IPipelineBehaviour<,>));

    private static void RegisterOpenGeneric(IServiceCollection services, Assembly[] assemblies, Type openInterfaceType)
    {
        var registered = new HashSet<(Type Service, Type Impl)>();

        foreach (var assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

            foreach (var impl in types.Where(t => t is { IsInterface: false, IsAbstract: false }))
            {
                foreach (var service in impl.GetInterfaces()
                             .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterfaceType))
                {
                    var pair = (Service: service, Impl: impl);
                    if (registered.Add(pair))
                        services.AddTransient(service, impl);
                }
            }
        }
    }

}
