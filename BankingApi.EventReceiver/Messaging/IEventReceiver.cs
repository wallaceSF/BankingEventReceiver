
namespace BankingApi.EventReceiver.Messaging;

public interface IEventReceiver
{
    Task<EventMessage?> PeekAsync(CancellationToken ct);
    Task CompleteAsync(EventMessage message, CancellationToken ct);
    Task AbandonAsync(EventMessage message, string reason, CancellationToken ct);
    Task DeadLetterAsync(EventMessage message, string reason, CancellationToken ct);
}
