using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace BankingApi.EventReceiver.Messaging;

public class AzureServiceBusEventReceiver : IEventReceiver, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusReceiver _receiver;
    private readonly ConcurrentDictionary<Guid, ServiceBusReceivedMessage> _inflight = new();

    public AzureServiceBusEventReceiver(ServiceBusClient client, string queueName)
    {
        _client = client;
        _receiver = _client.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
    }

    public async Task<EventMessage?> PeekAsync(CancellationToken ct)
    {
        var msg = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1), ct);
        if (msg is null)
        {
            return null;
        }

        var evt = msg.Body.ToObjectFromJson<EventMessage>(new JsonSerializerOptions
            { PropertyNameCaseInsensitive = true });

        _inflight[evt.Id] = msg;
        
        return evt;
    }

    public async Task CompleteAsync(EventMessage message, CancellationToken ct)
    {
        if (_inflight.TryRemove(message.Id, out var raw))
        {
            await _receiver.CompleteMessageAsync(raw, ct);
        }
    }

    public async Task AbandonAsync(EventMessage message, string reason, CancellationToken ct)
    {
        if (_inflight.TryRemove(message.Id, out var raw))
        {
            await _receiver.AbandonMessageAsync(raw, new Dictionary<string, object?> { ["reason"] = reason }, ct);
        }
    }

    public async Task DeadLetterAsync(EventMessage message, string reason, CancellationToken ct)
    {
        if (_inflight.TryRemove(message.Id, out var raw))
        {
            await _receiver.DeadLetterMessageAsync(raw, "NonTransient", reason, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _receiver.DisposeAsync();
        await _client.DisposeAsync();
    }
}