namespace EBus.Abstractions;

/// <summary>
/// Behaviors that run before/after the request handler.
/// </summary>
public interface IPipelineBehaviour<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Invoked around the handler. 
    /// next() calls the next behavior or the actual handler.
    /// </summary>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next);

}

/// <summary>
/// Delegate that represents the next step in the pipeline (either next behavior or final handler).
/// </summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();