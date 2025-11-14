using DoctorAppointmentSystem.Api.Extensions;
using DoctorAppointmentSystem.Api.Middleware;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Core.Shared;
using DoctorAppointmentSystem.Infrastructure.Data;
using DoctorAppointmentSystem.Infrastructure.Repositories;
using DoctorAppointmentSystem.Infrastructure.Services;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add Redis connection from Aspire
builder.AddRedisClient("redis");

builder.Services.AddSingleton<IDistributedLockProvider>(sp =>
{
    var connectionMultiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
    return new RedisDistributedSynchronizationProvider(connectionMultiplexer.GetDatabase());
});

// Register IDistributedCache using Redis
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

// Register Redis serial number service
builder.Services.AddSingleton<IRedisSerialNumberService, RedisSerialNumberService>();

// Register repositories
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IHospitalRepository, HospitalRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();

builder.Services.AddKeyedScoped<IAppointmentRepository, RedisAppointmentRepository>(AppointmentProviders.Redis);
builder.Services.AddKeyedScoped<IAppointmentRepository, PostgresAppointmentRepository>(AppointmentProviders.Postgres);

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Doctor Appointment System API",
        Version = "v1",
        Description = "Production-ready API with Redis distributed locking for atomic serial number assignment across multiple instances",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "API Support",
            Email = "support@appointer.com"
        }
    });
});

// Add CORS if needed
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
        options.RoutePrefix = string.Empty; // Swagger UI at root
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", async (AppDbContext dbContext) =>
{
    try
    {
        await dbContext.Database.CanConnectAsync();
        return Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            database = "Connected",
            redis = "Enabled"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Unhealthy",
            detail: $"Health check failed: {ex.Message}",
            statusCode: 503
        );
    }
})
    .WithName("HealthCheck")
    .WithTags("Health");

app.Run();
