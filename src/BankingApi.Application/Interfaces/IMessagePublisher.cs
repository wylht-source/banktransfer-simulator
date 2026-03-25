namespace BankingApi.Application.Interfaces;

public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the specified queue.
    /// Returns true if published successfully, false if failed.
    /// Failures are non-blocking — the caller should log and continue.
    /// </summary>
    Task<bool> PublishAsync(string queueName, object message, CancellationToken ct = default);
}