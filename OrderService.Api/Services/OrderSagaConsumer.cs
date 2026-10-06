using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Events;

namespace OrderService.Api.Consumers;

public class OrderSagaConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderSagaConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "saga_exchange";
    private const string QueueName = "order_saga_queue";

    public OrderSagaConsumer(IServiceProvider serviceProvider, ILogger<OrderSagaConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep retrying instead of crashing the whole API when RabbitMQ isn't up yet
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ unavailable ({Message}). Retrying in 10 seconds...", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(queue: QueueName, exchange: ExchangeName, routingKey: "payment.processed", cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queue: QueueName, exchange: ExchangeName, routingKey: "payment.failed", cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queue: QueueName, exchange: ExchangeName, routingKey: "inventory.failed", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            _logger.LogInformation("OrderSagaConsumer received routing key [{RoutingKey}]: {Message}", routingKey, message);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

                switch (routingKey)
                {
                    case "payment.processed":
                        var paymentEvent = JsonSerializer.Deserialize<PaymentProcessedEvent>(message);
                        if (paymentEvent != null)
                        {
                            await UpdateOrderStatusAsync(dbContext, paymentEvent.OrderId, "Confirmed");
                        }
                        break;

                    case "payment.failed":
                        var paymentFailedEvent = JsonSerializer.Deserialize<PaymentFailedEvent>(message);
                        if (paymentFailedEvent != null)
                        {
                            await UpdateOrderStatusAsync(dbContext, paymentFailedEvent.OrderId, "Cancelled");
                        }
                        break;

                    case "inventory.failed":
                        var inventoryFailedEvent = JsonSerializer.Deserialize<PaymentFailedEvent>(message);
                        if (inventoryFailedEvent != null)
                        {
                            await UpdateOrderStatusAsync(dbContext, inventoryFailedEvent.OrderId, "Cancelled");
                        }
                        break;
                }

                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event for routing key: {RoutingKey}", routingKey);
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task UpdateOrderStatusAsync(OrderDbContext dbContext, object orderId, string newStatus)
    {
        string targetIdStr = orderId.ToString() ?? string.Empty;

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.OrderId.ToString() == targetIdStr);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} was not found for status update to {Status}", orderId, newStatus);
            return;
        }

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Order {OrderId} status updated to {Status}", orderId, newStatus);
    }

    public override void Dispose()
    {
        _channel?.CloseAsync();
        _connection?.CloseAsync();
        base.Dispose();
    }
}