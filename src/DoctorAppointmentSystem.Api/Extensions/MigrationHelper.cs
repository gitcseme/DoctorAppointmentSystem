using Microsoft.EntityFrameworkCore;

namespace DoctorAppointmentSystem.Api.Extensions;

public static class MigrationHelper
{
    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<Program>>();
        try
        {
            var context = sp.GetRequiredService<Infrastructure.Data.AppDbContext>();

            logger.LogInformation("Applying database migrations...");
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database.");
            throw;
        }
    }
}
