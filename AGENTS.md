# AGENTS.md

## Run Commands

```bash
# Start everything (requires Docker running)
dotnet run --project src/DoctorAppointmentSystem.AppHost

# Run API only (requires services manually started)
dotnet run --project src/DoctorAppointmentSystem.Api

# Seed test data (requires PostgreSQL connection in appsettings.json)
dotnet run --project src/Tests/DoctorAppointmentSystem.ConsoleApp
```

## Configuration

Mode + Provider selection in `appsettings.json`:
```json
{
  "Appointment": {
    "Mode": "Sync",        // Sync | Async
    "Provider": "Postgres" // Postgres | Redis (for Sync), RabbitMq (for Async)
  }
}
```

### Available Combinations

| Mode | Provider | Endpoint | Response |
|------|----------|----------|----------|
| Sync | Postgres | POST /api/v1/appointments | 201 Created |
| Sync | Redis | POST /api/v1/appointments | 201 Created |
| Async | RabbitMq | POST /api/v2/appointments | 202 Accepted |

## Project Structure

```
src/
├── DoctorAppointmentSystem.Core/            # Domain, interfaces, DTOs, exceptions
├── DoctorAppointmentSystem.Application/     # Services and Processors
│   ├── Services/
│   │   ├── ISyncAppointmentService.cs
│   │   ├── SyncAppointmentService.cs
│   │   ├── IAsyncAppointmentService.cs
│   │   └── AsyncAppointmentService.cs
│   └── Processors/
│       ├── ISyncProcessor.cs
│       ├── PostgresAppointmentProcessor.cs
│       ├── RedisAppointmentProcessor.cs
│       ├── IAsyncProcessor.cs
│       └── RabbitMqAppointmentProcessor.cs
├── DoctorAppointmentSystem.Infrastructure/  # EF Core, repos, messaging, workers
├── DoctorAppointmentSystem.Api/              # Controllers
│   ├── V1AppointmentsController.cs          # Sync mode
│   └── V2AppointmentsController.cs           # Async mode
├── DoctorAppointmentSystem.AppHost/          # .NET Aspire orchestration
├── DoctorAppointmentSystem.ServiceDefaults/ # Shared configs
└── Tests/DoctorAppointmentSystem.ConsoleApp/ # Data seeding tool
```

## Endpoints

### V1 (Sync Mode)
- `POST /api/v1/appointments` - Create appointment (201 Created)
- `GET /api/v1/appointments/{id}` - Get by ID
- `GET /api/v1/appointments?doctorId=&hospitalId=&date=` - Get by doctor/date

### V2 (Async Mode)
- `POST /api/v2/appointments` - Create appointment (202 Accepted)
- `GET /api/v2/appointments/status/{reference}` - Get processing status
- `GET /api/v2/appointments/{id}` - Get by ID (after processing)
- `GET /api/v2/appointments?doctorId=&hospitalId=&date=` - Get by doctor/date

## Key Architecture Points

- **Entry point**: AppHost (not Api) - starts PostgreSQL, Redis, RabbitMQ via .NET Aspire
- **Mode-based registration**: Program.cs registers based on `Appointment:Mode` config
- **Processor pattern**: Services accept ISyncProcessor or IAsyncProcessor
- **Future extensibility**: Add AzureServiceBus/Kafka processors for Async mode
- **Migrations**: Auto-applied on API startup via `app.ApplyMigrations()`

## Important URLs (with AppHost running)

- Aspire Dashboard: `http://localhost:15000`
- API Swagger UI: Check Aspire dashboard for the API service endpoint
- Health Check: `GET /health`

## Dependencies

- .NET 9 SDK
- Docker Desktop (running) - required for AppHost
- PostgreSQL, Redis, RabbitMQ - provided by Aspire

## Notes

- V1 uses ISyncAppointmentService → ISyncProcessor (Postgres or Redis)
- V2 uses IAsyncAppointmentService → IAsyncProcessor (RabbitMq)
- In-flight marker prevents duplicate concurrent requests (via Redis)
- Switching Mode requires redeployment (not runtime changeable)