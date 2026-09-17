using DoctorAppointmentSystem.Api.Extensions;
using DoctorAppointmentSystem.Api.Middleware;
using DoctorAppointmentSystem.Application.Configuration;
using DoctorAppointmentSystem.Application.Processors;
using DoctorAppointmentSystem.Application.Services;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Infrastructure.Data;
using DoctorAppointmentSystem.Infrastructure.Repositories;
using DoctorAppointmentSystem.Infrastructure.Messaging;
using DoctorAppointmentSystem.Infrastructure.Services;
using DoctorAppointmentSystem.Infrastructure.Workers;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisClient("redis");

builder.AddRabbitMQClient("rabbitmq");

builder.Services.AddSingleton<IDistributedLockProvider>(sp =>
{
    var connectionMultiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
    return new RedisDistributedSynchronizationProvider(connectionMultiplexer.GetDatabase());
});

builder.Services.AddStackExchangeRedisCache(redisOpt =>
{
    var redis = builder.Configuration.GetConnectionString("redis");
    redisOpt.Configuration = redis;
});

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("appointments-db"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null)
    );

    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

builder.Services.Configure<AppointmentOptions>(
    builder.Configuration.GetSection(AppointmentOptions.SectionName));

builder.Services.AddSingleton<IRedisSerialNumberService, RedisSerialNumberService>();
builder.Services.AddSingleton<IAppointmentStatusTracker, RedisAppointmentStatusTracker>();
builder.Services.AddSingleton<IAppointmentMessagePublisher>(sp =>
{
    var connection = sp.GetRequiredService<IConnection>();
    return new RabbitMqAppointmentPublisher(connection);
});

builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IHospitalRepository, HospitalRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();

var options = builder.Configuration.GetSection(AppointmentOptions.SectionName).Get<AppointmentOptions>()
    ?? new AppointmentOptions();

if (options.Mode == "Sync")
{
    if (options.Provider == "Redis")
    {
        builder.Services.AddScoped<ISyncProcessor, RedisAppointmentProcessor>();
    }
    else if (options.Provider == "Postgres")
    {
        builder.Services.AddScoped<ISyncProcessor, PostgresAppointmentProcessor>();
    }

    builder.Services.AddScoped<ISyncAppointmentService, SyncAppointmentService>();
}
else // Async
{
    builder.Services.AddScoped<IAsyncProcessor, RabbitMqAppointmentProcessor>();
    builder.Services.AddScoped<IAsyncAppointmentService, AsyncAppointmentService>();
    builder.Services.AddHostedService<AppointmentConsumerWorker>();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swaggerOptions =>
{
    swaggerOptions.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Doctor Appointment System API",
        Version = "v1",
        Description = $"API with {options.Mode} mode using {options.Provider} provider",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "API Support",
            Email = "support@appointer.com"
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.ApplyMigrations();

app.MapDefaultEndpoints();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Doctor Appointment System API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", async (AppDbContext dbContext) =>
{
    try
    {
        await dbContext.Database.CanConnectAsync();
        return Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            database = "Connected"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Unhealthy",
            detail: $"Database connection failed: {ex.Message}",
            statusCode: 503
        );
    }
})
    .WithName("HealthCheck")
    .WithTags("Health");

app.Run();