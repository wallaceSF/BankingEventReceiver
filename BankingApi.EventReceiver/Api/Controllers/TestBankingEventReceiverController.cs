using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.EventReceiver.Api.Controllers;

[ApiController]
[Route("api-test")]
public class TestBankingEventReceiverController : ControllerBase
{
    private readonly IConfiguration _config;

    public TestBankingEventReceiverController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("balance")]
    public async Task<IActionResult> PostBalance([FromBody] BankMessage bankMessage)
    {
        await using var client = new ServiceBusClient(
            _config["ServiceBus:ConnectionString"],
            new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpTcp
            });

        var sender = client.CreateSender(_config["ServiceBus:QueueName"]);

        var json = JsonSerializer.Serialize(bankMessage);

        var message = new ServiceBusMessage(json);
        await sender.SendMessageAsync(message);

        return Ok();
    }

    public class BankMessage
    {
        public Guid Id { get; set; }
        public string MessageType { get; set; }
        public Guid BankAccountId { get; set; }
        public decimal Amount { get; set; }
    }
}