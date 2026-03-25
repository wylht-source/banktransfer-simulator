using System.Text.Json;
using Azure.Messaging.ServiceBus;
using BankingApi.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace BankingApi.Infrastructure.Services.Messaging;

public class ServiceBusMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusMessagePublisher> _logger;

    public ServiceBusMessagePublisher(
        ServiceBusClient client,
        ILogger<ServiceBusMessagePublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> PublishAsync(string queueName, object message, CancellationToken ct = default)
    {
        var sender = _client.CreateSender(queueName);

        try
        {
            var json       = JsonSerializer.Serialize(message);
            var busMessage = new ServiceBusMessage(json)
            {
                ContentType = "application/json"
            };

            await sender.SendMessageAsync(busMessage, ct);

            _logger.LogInformation(
                "Message published to queue '{Queue}': {MessageType}",
                queueName, message.GetType().Name);

            return true;
        }
        catch (Exception ex)
        {
            // Non-blocking — AI enrichment must not block loan creation
            _logger.LogError(ex,
                "Failed to publish message to queue '{Queue}'. Loan creation will proceed.",
                queueName);

            return false;
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}