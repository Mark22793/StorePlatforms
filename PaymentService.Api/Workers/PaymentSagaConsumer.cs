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
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost"
        };

        var connection = await factory.CreateConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync("order-saga-exchange", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);

        var queueName = "payment-saga-queue";
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        // Makikinig sa StockReservedEvent bago i-charge ang bayad
        await channel.QueueBindAsync(queueName, "order-saga-exchange", "stock.reserved", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
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

                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing payment saga event.");
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
            }
        };

        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
    }
}