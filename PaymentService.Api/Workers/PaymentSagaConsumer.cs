using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Data;
using PaymentService.Api.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Events;

namespace PaymentService.Api.Workers;

public class PaymentSagaConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentSagaConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public PaymentSagaConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<PaymentSagaConsumer> logger)
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

        var queueName = "payment-saga-queue";
        await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        // Makikinig sa StockReservedEvent bago i-charge ang bayad
        await _channel.QueueBindAsync(queueName, "order-saga-exchange", "stock.reserved", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            _logger.LogInformation("Payment Consumer received [{Key}]: {Message}", routingKey, message);

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<RabbitMqPublisher>();

                try
                {
                    if (routingKey == "stock.reserved")
                    {
                        var stockEvent = JsonSerializer.Deserialize<StockReservedEvent>(message);
                        if (stockEvent != null)
                        {
                            // Mock Payment Logic: Kung ang halaga ay higit sa 0 at mas mababa sa 100,000, SUCCESS.
                            bool isPaymentSuccessful = stockEvent.TotalPrice > 0 && stockEvent.TotalPrice < 100000;

                            var payment = new Payment
                            {
                                OrderId = stockEvent.OrderId,
                                Amount = stockEvent.TotalPrice,
                                Status = isPaymentSuccessful ? "Success" : "Failed",
                                FailureReason = isPaymentSuccessful ? null : "Transaction Limit Exceeded / Declined",
                                TransactionDate = DateTime.UtcNow
                            };

                            dbContext.Payments.Add(payment);
                            await dbContext.SaveChangesAsync(stoppingToken);

                            if (isPaymentSuccessful)
                            {
                                var processedEvent = new PaymentProcessedEvent(
                                    stockEvent.OrderId,
                                    stockEvent.TotalPrice,
                                    payment.Id.ToString(),
                                    stockEvent.CorrelationId
                                );
                                await publisher.PublishPaymentResultAsync(processedEvent, "payment.processed");
                            }
                            else
                            {
                                var failedEvent = new PaymentFailedEvent(
                                    stockEvent.OrderId,
                                    payment.FailureReason!,
                                    stockEvent.CorrelationId
                                );
                                await publisher.PublishPaymentResultAsync(failedEvent, "payment.failed");
                            }
                        }
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing payment saga event.");
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
