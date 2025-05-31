namespace EBus.Abstractions;

/// <summary>
/// Mediator interface: Dispatches requests (<see cref="IRequest{TResponse}"/>) and notifications (<see cref="INotification"/>).
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Send a request of type IRequest{TResponse} and get back TResponse.
    /// </summary>
    /// <typeparam name="TResponse">The response type that the request returns.</typeparam>
    /// <param name="request">An instance implementing IRequest{TResponse}.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a notification (INotification) to all registered handlers. You do not need to specify any generics.
    /// </summary>
    /// <param name="notification">An instance implementing INotification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Publish(INotification notification, CancellationToken cancellationToken = default);
}
