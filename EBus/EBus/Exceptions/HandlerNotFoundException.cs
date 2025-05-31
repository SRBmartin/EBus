namespace EBus.Exceptions;

/// <summary>
/// Thrown when no IRequestHandler{TRequest,TResponse} is registered for a given request type.
/// </summary>
public class HandlerNotFoundException : Exception
{
    public HandlerNotFoundException(Type requestType, Type handlerInterfaceType)
        : base($"No hanndler registered for request of type {requestType.FullName} (expected interface: {handlerInterfaceType.FullName}).") { }
}
