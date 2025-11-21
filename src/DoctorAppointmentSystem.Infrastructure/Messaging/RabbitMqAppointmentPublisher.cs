using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Interfaces;

namespace DoctorAppointmentSystem.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ publisher for appointment creation messages
/// Uses connection pooling and channel pooling for high throughput
/// </summary>
public class RabbitMqAppointmentPublisher : IAppointmentMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private const string ExchangeName = "appointments";
    private const string QueueName = "appointment-creation";
    private const string RoutingKey = "appointment.create";

    public RabbitMqAppointmentPublisher(IConnection connection)
    {
        _connection = connection;
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        
        // Declare exchange (topic type for future routing flexibility)
        _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false).GetAwaiter().GetResult();
        
        // Declare queue with high throughput settings
        var args = new Dictionary<string, object?>
        {
            // Enable lazy queue for better memory management under high load
            { "x-queue-mode", "lazy" },
            // Set max priority for potential priority handling
            { "x-max-priority", 10 }
        };
        
        _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args).GetAwaiter().GetResult();
        
        // Bind queue to exchange
        _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: RoutingKey).GetAwaiter().GetResult();
    }

    public async Task PublishAppointmentCreationAsync(
        AppointmentCreationMessage message, 
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true, // Persist messages to disk
            ContentType = "application/json",
            MessageId = message.AppointmentReference,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            DeliveryMode = DeliveryModes.Persistent
        };

        await _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        // Don't dispose connection - managed by DI container
    }
}
