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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "RabbitMQ unavailable ({Message}). Retrying in 10 seconds...",
                    ex.Message);

                await Task.Delay(
                    TimeSpan.FromSeconds(10),
                    stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost"
        };

        _connection =
            await factory.CreateConnectionAsync(stoppingToken);

        _channel =
            await _connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            "order-saga-exchange",
            ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);

        var queueName = "inventory-saga-queue";

        await _channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queueName,
            "order-saga-exchange",
            "order.created",
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queueName,
            "order-saga-exchange",
            "payment.failed",
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            _logger.LogInformation(
                "Inventory Consumer received [{Key}]: {Message}",
                routingKey,
                message);

            using var scope =
                _serviceProvider.CreateScope();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<InventoryDbContext>();

            var publisher =
                scope.ServiceProvider
                    .GetRequiredService<RabbitMqPublisher>();

            try
            {
                // ==========================================
                // ORDER CREATED
                // ==========================================
                if (routingKey == "order.created")
                {
                    var orderEvent =
                        JsonSerializer.Deserialize<OrderPlacedEvent>(
                            message);

                    if (orderEvent != null)
                    {
                        bool allItemsAvailable = true;

                        // Check ALL products first
                        foreach (var orderItem in orderEvent.Items)
                        {
                            var inventoryItem =
                                await dbContext.InventoryItems
                                    .FirstOrDefaultAsync(
                                        i => i.ProductId ==
                                             orderItem.ProductId,
                                        stoppingToken);

                            if (inventoryItem == null)
                            {
                                _logger.LogWarning(
                                    "Product not found in inventory: {ProductId}",
                                    orderItem.ProductId);

                                allItemsAvailable = false;
                                break;
                            }

                            if (inventoryItem.Quantity <
                                orderItem.Quantity)
                            {
                                _logger.LogWarning(
                                    "Insufficient stock for ProductId: {ProductId}. Available: {Available}, Requested: {Requested}",
                                    orderItem.ProductId,
                                    inventoryItem.Quantity,
                                    orderItem.Quantity);

                                allItemsAvailable = false;
                                break;
                            }
                        }

                        // ==========================================
                        // INSUFFICIENT STOCK
                        // ==========================================
                        if (!allItemsAvailable)
                        {
                            var failedEvent =
                                new InventoryFailedEvent(
                                    orderEvent.OrderId,
                                    "Insufficient stock available",
                                    orderEvent.CorrelationId
                                );

                            await publisher
                                .PublishInventoryResultAsync(
                                    failedEvent,
                                    "inventory.failed");
                        }
                        else
                        {
                            // ==========================================
                            // RESERVE ALL ITEMS
                            // ==========================================
                            foreach (var orderItem in orderEvent.Items)
                            {
                                var inventoryItem =
                                    await dbContext.InventoryItems
                                        .FirstOrDefaultAsync(
                                            i => i.ProductId ==
                                                 orderItem.ProductId,
                                            stoppingToken);

                                if (inventoryItem != null)
                                {
                                    inventoryItem.Quantity -=
                                        orderItem.Quantity;

                                    inventoryItem.ReservedQuantity +=
                                        orderItem.Quantity;

                                    inventoryItem.LastUpdated =
                                        DateTime.UtcNow;
                                }
                            }

                            await dbContext.SaveChangesAsync(
                                stoppingToken);

                            _logger.LogInformation(
                                "Stock Reserved for OrderId: {OrderId}",
                                orderEvent.OrderId);

                            // ==========================================
                            // PUBLISH STOCK RESERVED
                            // ==========================================
                            var reservedEvent =
                                new StockReservedEvent(
                                    orderEvent.OrderId,
                                    orderEvent.Items[0].ProductId,
                                    orderEvent.Items[0].Quantity,
                                    orderEvent.TotalAmount,
                                    orderEvent.CorrelationId
                                );

                            await publisher
                                .PublishInventoryResultAsync(
                                    reservedEvent,
                                    "stock.reserved");
                        }
                    }
                }

                // ==========================================
                // PAYMENT FAILED - COMPENSATION
                // ==========================================
                else if (routingKey == "payment.failed")
                {
                    var paymentFailedEvent =
                        JsonSerializer.Deserialize<PaymentFailedEvent>(
                            message);

                    if (paymentFailedEvent != null)
                    {
                        _logger.LogInformation(
                            "Payment failed for OrderId: {OrderId}. Executing Stock Release...",
                            paymentFailedEvent.OrderId);

                        // Compensation will be handled here.
                        // For now, acknowledge the event.

                        await dbContext.SaveChangesAsync(
                            stoppingToken);
                    }
                }

                await _channel.BasicAckAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing inventory saga event.");

                await _channel.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.CloseAsync();
        _connection?.CloseAsync();

        base.Dispose();
    }
}