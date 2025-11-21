using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Infrastructure.Workers;

/// <summary>
/// Background worker that consumes appointment creation messages from RabbitMQ
/// Supports multiple concurrent consumers for high throughput (1000+ req/s)
/// </summary>
public class AppointmentConsumerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppointmentConsumerWorker> _logger;
    private readonly IConnection _connection;
    private IChannel? _channel;
    private const string QueueName = "appointment-creation";
    private const ushort PrefetchCount = 50; // Process up to 50 messages concurrently per consumer

    public AppointmentConsumerWorker(
        IServiceProvider serviceProvider,
        ILogger<AppointmentConsumerWorker> logger,
        IConnection connection)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _connection = connection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Appointment Consumer Worker starting...");

        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        
        // Set QoS - prefetch multiple messages for better throughput
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<AppointmentCreationMessage>(json);

                if (message is null)
                {
                    _logger.LogWarning("Received null message, rejecting...");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                _logger.LogInformation(
                    "Processing appointment {Reference} - Serial {Serial} for Doctor-Hospital {DH} on {Date}",
                    message.AppointmentReference,
                    message.SerialNumber,
                    message.DoctorHospitalId,
                    message.AppointmentDate);

                // Process the message
                var success = await ProcessAppointmentAsync(message, stoppingToken);

                if (success)
                {
                    // Acknowledge message
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    _logger.LogInformation("Successfully processed appointment {Reference}", message.AppointmentReference);
                }
                else
                {
                    // Reject and requeue for retry (with dead-letter exchange configured, it will go to DLX after max retries)
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                    _logger.LogWarning("Failed to process appointment {Reference}, requeued", message.AppointmentReference);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false, // Manual acknowledgment for reliability
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Appointment Consumer Worker is running and consuming messages...");

        // Wait until cancellation is requested
        // Keep the worker alive
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cancellation requested, stopping consumer...");
        }
    }

    private async Task<bool> ProcessAppointmentAsync(
        AppointmentCreationMessage message,
        CancellationToken cancellationToken)
    {
        // Create a new scope for each message to get scoped services
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var statusTracker = scope.ServiceProvider.GetRequiredService<IAppointmentStatusTracker>();
        var redisSerialService = scope.ServiceProvider.GetRequiredService<IRedisSerialNumberService>();

        try
        {
            // Create the appointment in PostgreSQL
            var appointment = new Appointment
            {
                DoctorHospitalId = message.DoctorHospitalId,
                PatientId = message.PatientId,
                AppointmentDate = message.AppointmentDate,
                SerialNumber = message.SerialNumber,
                Status = AppointmentStatus.Scheduled,
                Notes = message.Notes,
                CreatedAt = DateTime.UtcNow
            };

            context.Appointments.Add(appointment);
            await context.SaveChangesAsync(cancellationToken);

            // Update status in Redis
            await statusTracker.SetCompletedAsync(
                message.AppointmentReference,
                appointment.Id,
                cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create appointment {Reference} in database",
                message.AppointmentReference);

            // Update status as failed
            await statusTracker.SetFailedAsync(
                message.AppointmentReference,
                ex.Message,
                cancellationToken);

            // Decrement the Redis counter since we failed to create the appointment
            //await redisSerialService.DecrementSerialNumberAsync(
            //    message.DoctorHospitalId,
            //    message.AppointmentDate,
            //    cancellationToken);

            return false;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Appointment Consumer Worker stopping...");
        
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
