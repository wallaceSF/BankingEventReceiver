
namespace BankingApi.EventReceiver.Messaging;

public sealed class EventMessage
{
    public Guid Id { get; init; }
    public string MessageType { get; init; } = string.Empty;
    public Guid BankAccountId { get; init; }
    public decimal Amount { get; init; }
}
