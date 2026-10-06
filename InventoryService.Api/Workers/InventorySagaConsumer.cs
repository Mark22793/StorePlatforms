using System.Text;
using System.Text.Json;
using InventoryService.Api.Data;
using InventoryService.Api.Services;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Events;

namespace InventoryService.Api.Workers;

public class InventorySagaConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InventorySagaConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public InventorySagaConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<InventorySagaConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
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
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync("order-saga-exchange", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);

        var queueName = "inventory-saga-queue";
        await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(queueName, "order-saga-exchange", "order.created", cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queueName, "order-saga-exchange", "payment.failed", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            _logger.LogInformation("Inventory Consumer received [{Key}]: {Message}", routingKey, message);

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<RabbitMqPublisher>();

                try
                {
                    if (routingKey == "order.created")
                    {
                        var orderEvent = JsonSerializer.Deserialize<OrderPlacedEvent>(message);
                        if (orderEvent != null)
                        {
                            // 1. Stock Reservation Logic
                            var item = await dbContext.InventoryItems
                                .FirstOrDefaultAsync(i => i.ProductId == orderEvent.ProductId, cancellationToken: stoppingToken);

                            if (item != null && item.Quantity >= orderEvent.Quantity)
                            {
                                item.Quantity -= orderEvent.Quantity;
                                item.ReservedQuantity += orderEvent.Quantity;
                                await dbContext.SaveChangesAsync(stoppingToken);

                                _logger.LogInformation("Stock Reserved for OrderId: {OrderId}", orderEvent.OrderId);

                                // Mag-publish ng StockReservedEvent para ipagpatuloy ang Saga sa Payment
                                var reservedEvent = new StockReservedEvent(
                                    orderEvent.OrderId,
                                    orderEvent.ProductId,
                                    orderEvent.Quantity,
                                    orderEvent.TotalAmount,
                                    orderEvent.CorrelationId
                                );
                                await publisher.PublishInventoryResultAsync(reservedEvent, "stock.reserved");
                            }
                            else
                            {
                                _logger.LogWarning("Insufficient stock for ProductId: {ProductId}", orderEvent.ProductId);

                                // Mag-publish ng InventoryFailedEvent (Kulang ang stock)
                                var failedEvent = new InventoryFailedEvent(
                                    orderEvent.OrderId,
                                    "Insufficient stock available",
                                    orderEvent.CorrelationId
                                );
                                await publisher.PublishInventoryResultAsync(failedEvent, "inventory.failed");
                            }
                        }
                    }
                    else if (routingKey == "payment.failed")
                    {
                        var paymentFailedEvent = JsonSerializer.Deserialize<PaymentFailedEvent>(message);
                        if (paymentFailedEvent != null)
                        {
                            // 2. Compensation Logic (Release Stock kapag pumalya ang Payment)
                            _logger.LogInformation("Payment failed for OrderId: {OrderId}. Executing Stock Release...", paymentFailedEvent.OrderId);

                            // Dito ire-release ang dating reserved stock pabalik sa available stock
                            // Note: Maaari mong i-adjust depende sa model ng iyong InventoryItem
                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing inventory saga event.");
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
            }
        };

        await _channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        // Keep the connection alive so the consumer keeps receiving
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.CloseAsync();
        _connection?.CloseAsync();
        base.Dispose();
    }
}
