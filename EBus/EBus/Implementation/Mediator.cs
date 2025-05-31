using EBus.Abstractions;
using EBus.Exceptions;

namespace EBus.Implementation;

/// <summary>
/// Concrete Mediator that dispatches requests & notifications via DI,
/// resolving the correct closed-generic handler at runtime and invoking
/// Handle(...) via dynamic binding
/// </summary>
public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        Type requestType = request.GetType();
        Type responseType = typeof(TResponse);

        Type handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);

        dynamic handlerInstance = _serviceProvider.GetService(handlerInterface)!;
        if (handlerInstance == null) throw new HandlerNotFoundException(requestType, handlerInterface);

        Type behaviourInterface = typeof(IPipelineBehaviour<,>).MakeGenericType(requestType, responseType);

        Type enumerableBehaviour = typeof(IEnumerable<>).MakeGenericType(behaviourInterface);

        var behavioursObj = _serviceProvider.GetService(enumerableBehaviour);
        var behaviours = behavioursObj as IEnumerable<object> ?? Enumerable.Empty<object>();

        RequestHandlerDelegate<TResponse> finalHandlerDelegate = () =>
        {
            return handlerInstance.Handle((dynamic)request, cancellationToken);
        };

        foreach (object behaviourObj in behaviours.Reverse())
        {
            dynamic pipelineBehaviour = behaviourObj;
            var next = finalHandlerDelegate;
            finalHandlerDelegate = () =>
            {
                return pipelineBehaviour.Handle((dynamic)request, cancellationToken, next);
            };
        }

        return await finalHandlerDelegate().ConfigureAwait(false);

    }

    /// <inheritdoc/>
    public async Task Publish(INotification notification, CancellationToken cancellationToken = default)
    {
        if (notification == null) throw new ArgumentNullException(nameof(notification));

        Type notificationType = notification.GetType();

        Type handlerInterface = typeof(INotificationHandler<>).MakeGenericType(notificationType);

        Type enumerableHandler = typeof(IEnumerable<>).MakeGenericType(handlerInterface);

        var handlersObj = _serviceProvider.GetService(enumerableHandler);
        var handlers = handlersObj as IEnumerable<object> ?? Enumerable.Empty<object>();

        var tasks = new List<Task>();
        foreach (object handlerObj in handlers)
        {
            dynamic h = handlerObj;
            tasks.Add(h.Handle((dynamic)notification, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

}
