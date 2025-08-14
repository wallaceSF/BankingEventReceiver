
namespace BankingApi.EventReceiver.Domain;

public class ProcessedMessage
{
    public Guid Id { get; set; }
    public DateTime ProcessedAt { get; set; }
}
