# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Appointer** (Doctor Appointment System) — a .NET 9 Web API for managing doctor appointments, backed by PostgreSQL, with serial-based (not time-slot) appointment numbering: appointments are numbered sequentially (1, 2, 3…) per doctor/hospital/day.

## Run / build commands

```bash
# Build the solution
dotnet build DoctorAppointmentSystem.sln

# Start everything, incl. Postgres/Redis/RabbitMQ containers (requires Docker Desktop running)
dotnet run --project src/DoctorAppointmentSystem.AppHost
# -> Aspire dashboard at http://localhost:15000

# Run the API only (if Postgres/Redis/RabbitMQ are already running elsewhere)
dotnet run --project src/DoctorAppointmentSystem.Api

# Seed test data with Bogus (needs a Postgres connection string in its own appsettings.json)
dotnet run --project src/Tests/DoctorAppointmentSystem.ConsoleApp
```

There is no unit/integration test project in the solution — `src/Tests/DoctorAppointmentSystem.ConsoleApp` is a Bogus-based data seeder only, not a test suite. No `dotnet test` target exists.

### Load testing (k6)

```bash
k6 run k6/load-test.js            # sync flow
k6 run k6/rabbitmq-load-test.js   # async/RabbitMQ flow
```

Override target with `API_URL` env var (default `https://localhost:7123`). **Known issue:** both scripts still POST to the old unversioned path `/api/appointments` — update to `/api/v1/appointments` (sync) or `/api/v2/appointments` (async) before relying on results.

## Architecture

Clean Architecture layering, `src/`:

- `DoctorAppointmentSystem.Core` — entities (`Doctor`, `Hospital`, `DoctorHospital`, `Patient`, `Appointment`, `AppointmentCounter`), interfaces, DTOs, exceptions. Framework-agnostic.
- `DoctorAppointmentSystem.Application` — business logic:
  - `Services/`: `ISyncAppointmentService`/`SyncAppointmentService`, `IAsyncAppointmentService`/`AsyncAppointmentService`
  - `Processors/`: `ISyncProcessor` (`PostgresAppointmentProcessor`, `RedisAppointmentProcessor`), `IAsyncProcessor` (`RabbitMqAppointmentProcessor`)
  - `Configuration/AppointmentOptions` — binds the `Appointment:Mode`/`Appointment:Provider` config
- `DoctorAppointmentSystem.Infrastructure` — EF Core (`Data/`, `Migrations/`), `Repositories/`, `Messaging/` (RabbitMQ publisher), `Workers/` (RabbitMQ consumer), `Services/` (Redis-backed)
- `DoctorAppointmentSystem.Api` — `Controllers/`, `Middleware/` (global exception handling), `Extensions/`, `Program.cs` (DI wiring, mode branching)
- `DoctorAppointmentSystem.AppHost` — .NET Aspire orchestration; this is the actual entry point for local dev, not `Api`. Declares Postgres (`appointments-db` + PgAdmin), Redis, and RabbitMQ (with management plugin) as Aspire resources.
- `DoctorAppointmentSystem.ServiceDefaults` — shared Aspire service defaults

### Sync vs Async split

Controlled entirely by `Appointment:Mode` (`Sync`|`Async`) and `Appointment:Provider` (`Postgres`|`Redis` for Sync, `RabbitMq` for Async) in `appsettings.json`. `Program.cs` reads this once at startup and branches DI registration accordingly — **switching modes requires a redeploy**, it is not hot-reloadable.

| Mode | Provider | Endpoint | Response |
|------|----------|----------|----------|
| Sync | Postgres | `POST /api/v1/appointments` | 201 Created |
| Sync | Redis | `POST /api/v1/appointments` | 201 Created |
| Async | RabbitMq | `POST /api/v2/appointments` | 202 Accepted |

- **Sync mode**: registers `ISyncProcessor` + `ISyncAppointmentService`, exposed via `V1AppointmentsController` (`api/v1/appointments`). Creation happens synchronously in the request.
  - `PostgresAppointmentProcessor`: atomic serial-number assignment via a single-row `FOR UPDATE` lock on the `appointment_counters` table — O(1), avoids `MAX()`/`COUNT()` scans over appointments.
  - `RedisAppointmentProcessor`: distributed lock via `Medallion.Threading.Redis` instead of a DB row lock.
- **Async mode**: registers `IAsyncProcessor` (`RabbitMqAppointmentProcessor`) + `IAsyncAppointmentService` + `AppointmentConsumerWorker` (a `BackgroundService`, added via `AddHostedService`), exposed via `V2AppointmentsController` (`api/v2/appointments`). Flow: client POSTs → publisher publishes to RabbitMQ and returns 202 with an `appointmentReference` + status URL → client polls `GET /api/v2/appointments/status/{reference}` → consumer creates the `Appointment` row in a fresh DI scope and updates status via `IAppointmentStatusTracker` (Redis-backed).
  - RabbitMQ topology: topic exchange `appointments`, queue `appointment-creation`, routing key `appointment.create`, lazy queue mode, `x-max-priority: 10`, persistent messages.
  - Consumer uses prefetch count 50 (tuned for 1000+ req/s), manual ack/nack.

API versioning is plain route-prefix versioning via distinctly-named controllers (`V1AppointmentsController`, `V2AppointmentsController`), not the `Microsoft.AspNetCore.Mvc.Versioning` package. `DoctorsController`, `HospitalsController`, `PatientsController` are unversioned.

### Persistence

EF Core + Npgsql, `AppDbContext` in `Infrastructure/Data/`. Migrations auto-apply on API startup via `app.ApplyMigrations()`. Retry policy: 3 retries, 5s max delay.

Redis is used for two independent purposes: distributed locking for the Sync/Redis processor path, and `IAppointmentStatusTracker` for async job status + in-flight duplicate-request markers (a patient cannot have two concurrent pending requests — see `SetCompletedAsync`/`SetFailedAsync`).

## Extending

Adding a new async transport (e.g. Azure Service Bus, Kafka): implement `IAsyncProcessor`, register it under the `Appointment:Provider` switch in `Program.cs`. Adding a new sync backend: implement `ISyncProcessor` the same way.
