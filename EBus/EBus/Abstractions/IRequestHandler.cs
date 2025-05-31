namespace EBus.Abstractions;

/// <summary>
/// Defines a handler for TRequest that returns TResponse.
/// </summary>
public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}
