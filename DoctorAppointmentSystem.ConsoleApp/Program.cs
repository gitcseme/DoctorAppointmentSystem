using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DoctorAppointmentSystem.Infrastructure.Data;
using DoctorAppointmentSystem.ConsoleApp;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Setup dependency injection
var services = new ServiceCollection();

// Add DbContext
var connectionString = configuration.GetConnectionString("DefaultConnection");
services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.EnableSensitiveDataLogging();
    options.EnableDetailedErrors();
});

// Add DataSeeder
services.AddScoped<DataSeeder>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

try
{
    using var scope = serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();

    // Test database connection
    Console.WriteLine("Testing database connection...");
    if (await context.Database.CanConnectAsync())
    {
        Console.WriteLine("? Database connection successful!");
        Console.WriteLine();
    }
    else
    {
        Console.WriteLine("? Cannot connect to database. Please check your connection string.");
        return;
    }

    // Seed data
    await seeder.SeedDataAsync();

    // Display statistics
    await seeder.DisplayStatisticsAsync();
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("? An error occurred:");
    Console.WriteLine(ex.Message);
    Console.WriteLine();
    Console.WriteLine("Stack Trace:");
    Console.WriteLine(ex.StackTrace);
}

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
