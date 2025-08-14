using System.Data;
using BankingApi.EventReceiver.Domain;
using BankingApi.EventReceiver.Infrastructure;
using BankingApi.EventReceiver.Messaging;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace BankingApi.EventReceiver.Worker;

public class MessageWorker : BackgroundService
{
    private readonly ILogger<MessageWorker> _logger;
    private readonly IServiceProvider _services;
    private readonly IEventReceiver _receiver;

    private static readonly TimeSpan[] Backoffs = new[]
    {
        //TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(125)
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(5)
    };

    private readonly AsyncRetryPolicy _retry = Policy
        .Handle<TimeoutException>()
        .Or<DbUpdateException>(IsTransientDbError)
        .Or<IOException>()
        .WaitAndRetryAsync(Backoffs);

    public MessageWorker(ILogger<MessageWorker> logger, IServiceProvider services, IEventReceiver receiver)
    {
        _logger = logger;
        _services = services;
        _receiver = receiver;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageWorker started.");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            EventMessage? msg = null;

            try
            {
                msg = await _receiver.PeekAsync(stoppingToken);
                if (msg is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    continue;
                }

                try
                {
                    await _retry.ExecuteAsync(async ct =>
                    {
                        var result = await ProcessAtomicAsync(msg, ct);
                        
                        if (!result.Success)
                        {
                            if (result.IsTransient)
                            {
                                await _receiver.AbandonAsync(msg, result.Reason ?? "Transient", ct);
                                throw new TimeoutException(result.Reason ?? "Transient");
                            }

                            await _receiver.DeadLetterAsync(msg, result.Reason ?? "Non-transient", ct);
                            return;
                        }

                        await _receiver.CompleteAsync(msg, ct);
                    }, stoppingToken);
                }
                catch (TimeoutException) {}
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (msg is not null)
                {
                    _logger.LogError(ex, "Fatal outside pipeline for {MessageId}. Dead-lettering.", msg.Id);
                    await _receiver.DeadLetterAsync(msg, "Fatal outside pipeline", stoppingToken);
                }
                else
                {
                    _logger.LogError(ex, "Fatal while peeking messages.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        _logger.LogInformation("MessageWorker stopped.");
    }

    private static bool IsTransientDbError(DbUpdateException ex)
    {
        var s = ex.ToString().ToLowerInvariant();
        return s.Contains("deadlock") || s.Contains("timeout");
    }

    private async Task<(bool Success, bool IsTransient, string? Reason)> ProcessAtomicAsync(Messaging.EventMessage msg,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, 
                cancellationToken);

            try
            {
                db.ProcessedMessages.Add(new ProcessedMessage { Id = msg.Id });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException dupEx)
            {
                var text = dupEx.ToString().ToLowerInvariant();
                
                if (text.Contains("duplicate") || text.Contains("primary key") || text.Contains("unique"))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return (true, false, null);
                }

                throw;
            }

            var delta = msg.MessageType?.Equals(MessageTypes.Credit, StringComparison.OrdinalIgnoreCase) == true
                ? msg.Amount
                : msg.MessageType?.Equals(MessageTypes.Debit, StringComparison.OrdinalIgnoreCase) == true
                    ? -msg.Amount
                    : (decimal?)null;

            if (delta is null)
            {
                return (false, false, $"Unsupported message type: {msg.MessageType}");
            }

            var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE BankAccounts WITH (ROWLOCK)
                SET Balance = Balance + {delta.Value}
                WHERE Id = {msg.BankAccountId};", cancellationToken);

            if (rows == 0)
            {
                return (false, false, $"BankAccount not found: {msg.BankAccountId}");
            }

            await transaction.CommitAsync(cancellationToken);
            
            return (true, false, null);
        }
        catch (DbUpdateException ex) when (IsTransientDbError(ex))
        {
            return (false, true, ex.Message);
        }
        catch (TimeoutException ex)
        {
            return (false, true, ex.Message);
        }
        catch (Exception ex)
        {
            return (false, false, ex.Message);
        }
    }
}