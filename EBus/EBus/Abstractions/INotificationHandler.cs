namespace EBus.Abstractions;

/// <summary>
/// Handles a notification of type TNotification.
/// </summary>
public interface INotificationHandler<TNotification> where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken = default);
}
