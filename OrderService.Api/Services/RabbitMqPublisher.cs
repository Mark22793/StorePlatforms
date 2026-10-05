using System.Text;
using System.Text.Json;
using OrderService.Api.Events;
using RabbitMQ.Client;

namespace OrderService.Api.Services;

public class RabbitMqPublisher
{
    private readonly IConfiguration _configuration;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task PublishOrderPlacedAsync(
        OrderPlacedEvent orderPlacedEvent)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
            UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        await using var connection =
            await factory.CreateConnectionAsync();

        await using var channel =
            await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: "store.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        var body = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(orderPlacedEvent));

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = "OrderPlaced"
        };

        await channel.BasicPublishAsync(
            exchange: "store.events",
            routingKey: "order.placed",
            mandatory: false,
            basicProperties: properties,
            body: body);
    }
}