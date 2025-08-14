
namespace BankingApi.EventReceiver.Domain;

public class BankAccount
{
    public Guid Id { get; set; }
    public decimal Balance { get; set; }
    public byte[] RowVersion { get; set; } = System.Array.Empty<byte>();
}
