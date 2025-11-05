# Doctor Appointment System

A production-ready .NET 9 Web API for managing doctor appointments with PostgreSQL, featuring atomic serial number assignment and optimized concurrency control using a dedicated counter table.

## Features

- **Serial-Based Appointments**: Appointments use sequential serial numbers (1, 2, 3...) instead of time slots
- **Multi-Hospital Support**: Doctors can serve in multiple hospitals with different daily patient limits
- **Optimized Concurrency Control**: Single-row locking using `AppointmentCounter` table for O(1) performance
- **PostgreSQL Row-Level Locking**: `FOR UPDATE` ensures atomic operations
- **Daily Limit Enforcement**: Automatically rejects appointments when daily patient limit is reached
- **Production-Ready**: Complete with error handling, global exception middleware, health checks, and Swagger API documentation
- **.NET Aspire Orchestration**: Integrated service defaults and orchestration support

## Architecture

The solution follows Clean Architecture principles with three layers:

```
src/
├── DoctorAppointmentSystem.Core/        # Domain entities, interfaces, DTOs, exceptions
├── DoctorAppointmentSystem.Infrastructure/ # EF Core, repositories, database access
├── DoctorAppointmentSystem.Api/ # Web API controllers, middleware
├── DoctorAppointmentSystem.AppHost/       # .NET Aspire orchestration
└── DoctorAppointmentSystem.ServiceDefaults/ # Shared service configurations
```

### Key Design Decisions

1. **AppointmentCounter Table**: Dedicated counter table for efficient single-row locking per doctor-hospital-date
2. **Row-Level Locking**: Uses PostgreSQL `FOR UPDATE` to lock the counter row during operations
3. **Transaction Isolation**: Uses `ReadCommitted` isolation level with execution strategy for retry logic
4. **Repository Pattern**: Clean separation between business logic and data access
5. **Global Exception Handling**: Middleware-based consistent error responses
6. **.NET Aspire**: Service discovery, health checks, and orchestration

## 🚀 Getting Started

### Prerequisites

- .NET 9 SDK
- Docker Desktop (running)
- Visual Studio 2022 or VS Code with C# Dev Kit

### Running the Application

1. **Clone the repository**
   ```bash
   git clone https://github.com/gitcseme/DoctorAppointmentSystem
   cd DoctorAppointmentSystem
   ```

2. **Ensure Docker is running**

3. **Run the .NET Aspire AppHost**
   ```bash
   dotnet run --project src/DoctorAppointmentSystem.AppHost
   ```
   
   Or run from Visual Studio by setting `DoctorAppointmentSystem.AppHost` as the startup project.

4. **Access the application**
   - **Aspire Dashboard**: Opens automatically (monitors all services)
   - **API Swagger UI**: Check Aspire dashboard for the API service endpoint
   - **Health Check**: `GET /health` endpoint

The application will automatically:
- Start PostgreSQL in a container
- Apply database migrations
- Configure connection strings
- Start the API service

## Database Schema

### Core Tables

- **doctors**: Doctor information (name, specialization, email, phone)
- **hospitals**: Hospital information (name, address, city, phone)
- **doctor_hospitals**: Many-to-many relationship with `daily_patient_limit`
- **patients**: Patient information (name, email, phone, DOB, address)
- **appointments**: Appointments with `serial_number` per doctor/hospital/date
- **appointment_counters**: ⚡ **Optimized counter table** for tracking serials and counts

### AppointmentCounter Table (Key Innovation)

The `appointment_counters` table enables **O(1) performance** by storing:

```sql
CREATE TABLE appointment_counters (
    id SERIAL PRIMARY KEY,
    doctor_hospital_id INT NOT NULL,
    appointment_date DATE NOT NULL,
    current_serial INT NOT NULL DEFAULT 0,
    appointment_count INT NOT NULL DEFAULT 0,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(doctor_hospital_id, appointment_date)
);
```

**Benefits**:
- Locks only **1 row** instead of N appointment rows
- No `MAX(serial_number)` calculation needed
- No `COUNT(*)` query needed
- Constant-time performance regardless of appointment volume

### Key Constraints

- Unique index on `(doctor_hospital_id, appointment_date, serial_number)`
- Unique index on `(doctor_hospital_id, patient_id, appointment_date)` - prevents duplicate bookings
- Unique index on `(doctor_hospital_id, appointment_date)` in counter table
- Foreign keys with appropriate cascade/restrict behaviors

## ⚡ Concurrency Handling

### How It Works (Optimized)

When multiple requests try to book appointments simultaneously:

1. **Lock Counter Row**: Acquires `FOR UPDATE` lock on single counter row for doctor-hospital-date
2. **Get/Create Counter**: If first appointment of the day, creates counter row
3. **Check Daily Limit**: Validates `appointment_count < daily_patient_limit`
4. **Atomic Increment**: Increments both `current_serial` and `appointment_count`
5. **Create Appointment**: Inserts appointment with the new serial number
6. **Commit Transaction**: Releases the lock

### Race Condition Prevention

- PostgreSQL `FOR UPDATE` provides pessimistic locking
- Transaction isolation ensures ACID compliance
- Unique constraints prevent duplicate serials at database level
- Execution strategy handles transient failures with automatic retry
- Counter table grows by only 1 row per doctor-hospital-date (minimal overhead)

### Standard HTTP Status Codes

| Status Code | Scenario |
|-------------|----------|
| 200 OK | Successful GET request |
| 201 Created | Appointment created successfully |
| 400 Bad Request | Invalid request data |
| 404 Not Found | Resource not found (doctor, hospital, patient) |
| 409 Conflict | Daily limit reached or duplicate booking attempt |
| 500 Internal Server Error | Unexpected server error |
| 503 Service Unavailable | Health check failure (database disconnected) |

## 🔧 Configuration

### Connection String

The application uses .NET Aspire for service orchestration. Connection strings are automatically configured for:
- PostgreSQL database (`appointments-db`)
- Service discovery
- Health checks

### Retry Policy

EF Core is configured with retry logic:
- Max retry count: 3
- Max retry delay: 5 seconds
- Handles transient PostgreSQL failures

### CORS

Currently configured with `AllowAll` policy for development. Update in `Program.cs` for production:

## 📊 Performance Considerations

- **Connection Pooling**: Npgsql automatically pools connections
- **Async/Await**: All database operations are fully asynchronous
- **Strategic Indexing**: Indexes on foreign keys and unique constraints
- **Minimal Lock Duration**: Transactions kept as short as possible
- **O(1) Counter Operations**: Constant-time serial assignment regardless of scale
- **Execution Strategy**: Automatic retry on transient failures
- **Sensitive Data Logging**: Enabled only in Development mode

**Expected Behavior**:
- First N requests (up to `daily_patient_limit`) succeed with sequential serials
- Remaining requests receive `409 Conflict` with "Daily limit reached" message
- All successful appointments have unique, sequential serial numbers (no gaps or duplicates)

## 🏗️ Project Structure

```
DoctorAppointmentSystem/ 
├── src/
│   ├── DoctorAppointmentSystem.Core/
│   │   ├── Entities/ # Domain models (Doctor, Hospital, Appointment, AppointmentCounter)
│   │   ├── Interfaces/  # Repository contracts
│ │   ├── DTOs/  # API request/response models
│   │   └── Exceptions/      # Custom exceptions
│   ├── DoctorAppointmentSystem.Infrastructure/
│   │   ├── Data/   # DbContext, EF Core configuration
│   │   ├── Repositories/      # Repository implementations
│   │   └── Migrations/     # EF Core migrations
│   ├── DoctorAppointmentSystem.Api/
│   │   ├── Controllers/       # API endpoints
│   │   ├── Middleware/   # Global exception handler
│   │   └── Extensions/        # Migration extensions
│   ├── DoctorAppointmentSystem.AppHost/
│   │   └── Program.cs         # .NET Aspire orchestration
│   └── DoctorAppointmentSystem.ServiceDefaults/
│       └── Extensions.cs      # Shared service configurations
└── README.md
```

## 🔄 Database Migrations

Migrations are automatically applied on application startup via `app.ApplyMigrations()` extension.

### Health

- `GET /health` - Health check (database connectivity)

### Documentation

- Swagger UI available at root (`/`) in Development mode

## 🚧 Future Enhancements

- [ ] Appointment cancellation endpoint (with counter decrement)
- [ ] Appointment status updates (Completed, No-Show)
- [ ] Doctor availability/schedule management
- [ ] Notification system (email/SMS for appointment reminders)
- [ ] Authentication & Authorization (JWT/OAuth)
- [ ] Rate limiting middleware
- [ ] Caching layer (Redis for read-heavy operations)
- [ ] Admin dashboard
- [ ] Audit logging
- [ ] Appointment rescheduling

## 📄 License

This project is available for educational and commercial use.

---

**Built with ❤️ using .NET 9, PostgreSQL, EF Core, and .NET Aspire**
