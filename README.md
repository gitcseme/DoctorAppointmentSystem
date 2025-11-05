# Doctor Appointment System

A production-ready .NET 9 Web API for managing doctor appointments with PostgreSQL, featuring atomic serial number assignment and robust concurrency control.

## ?? Features

- **Serial-Based Appointments**: Appointments use sequential serial numbers (1, 2, 3...) instead of time slots
- **Multi-Hospital Support**: Doctors can serve in multiple hospitals with different daily patient limits
- **Concurrency Control**: PostgreSQL row-level locking (`FOR UPDATE`) ensures atomic serial number assignment
- **Daily Limit Enforcement**: Automatically rejects appointments when daily patient limit is reached
- **Production-Ready**: Complete with error handling, logging, health checks, and API documentation

## ??? Architecture

The solution follows Clean Architecture principles with three layers:

```
src/
??? DoctorAppointmentSystem.Core/          # Domain entities, interfaces, DTOs, exceptions
??? DoctorAppointmentSystem.Infrastructure/ # EF Core, repositories, database access
??? DoctorAppointmentSystem.Api/    # Web API controllers, middleware
```

### Key Design Decisions

1. **Row-Level Locking**: Uses PostgreSQL `FOR UPDATE` to lock rows during serial number assignment
2. **Transaction Isolation**: Uses `ReadCommitted` isolation level with retry logic
3. **Repository Pattern**: Clean separation between business logic and data access
4. **Exception Handling**: Global middleware for consistent error responses

## ?? Getting Started

### Prerequisites

- Using .NET Aspire orchestration
- Docker installed
- .NET 9 SDK
- Visual Studio 2022 or VS Code with C# extension

### Application Setup

1. **Clone/Open the repository**
2. **Install docker & keep it running**
3. **Run the aspire app**


5. **Access Swagger UI**:
- Open browser: `https://localhost:5001` or `http://localhost:5000`
   - Swagger documentation will be available at the root URL

## ?? Database Schema

### Core Tables

- **doctors**: Doctor information (name, specialization, contact)
- **hospitals**: Hospital information (name, location, contact)
- **doctor_hospitals**: Many-to-many relationship with `daily_patient_limit`
- **patients**: Patient information
- **appointments**: Appointments with `serial_number` per doctor/hospital/date

### Key Constraints

- Unique index on `(doctor_hospital_id, appointment_date, serial_number)`
- Unique index on `(doctor_hospital_id, patient_id, appointment_date)` - prevents duplicate bookings
- Foreign keys with appropriate delete behaviors

## ?? API Endpoints

### Doctors

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/doctors` | Create a new doctor |
| GET | `/api/doctors` | Get all doctors |
| GET | `/api/doctors/{id}` | Get doctor by ID |
| POST | `/api/doctors/assign-to-hospital` | Assign doctor to hospital |
| GET | `/api/doctors/{doctorId}/hospitals/{hospitalId}` | Get doctor-hospital details |

### Hospitals

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/hospitals` | Create a new hospital |
| GET | `/api/hospitals` | Get all hospitals |
| GET | `/api/hospitals/{id}` | Get hospital by ID |
| GET | `/api/hospitals/{id}/doctors` | Get all doctors at hospital |

### Patients

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/patients` | Create a new patient |
| GET | `/api/patients` | Get all patients |
| GET | `/api/patients/{id}` | Get patient by ID |

### Appointments

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/appointments` | Book a new appointment |
| GET | `/api/appointments/{id}` | Get appointment by ID |
| GET | `/api/appointments?doctorId={id}&hospitalId={id}&date={date}` | Get appointments by doctor/hospital/date |
| GET | `/api/appointments/doctor/{doctorId}/hospital/{hospitalId}/date/{date}` | Alternative route for appointments |

### Health Check

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Check API and database health |

## ?? Example Usage

### 1. Create a Doctor

```bash
POST /api/doctors
Content-Type: application/json

{
  "name": "Dr. John Smith",
  "specialization": "Cardiology",
  "email": "john.smith@example.com",
  "phoneNumber": "+1234567890"
}
```

### 2. Create a Hospital

```bash
POST /api/hospitals
Content-Type: application/json

{
  "name": "City General Hospital",
  "address": "123 Main St",
  "city": "New York",
  "phoneNumber": "+1234567891"
}
```

### 3. Assign Doctor to Hospital

```bash
POST /api/doctors/assign-to-hospital
Content-Type: application/json

{
  "doctorId": 1,
  "hospitalId": 1,
  "dailyPatientLimit": 20
}
```

### 4. Create a Patient

```bash
POST /api/patients
Content-Type: application/json

{
  "name": "Jane Doe",
  "email": "jane.doe@example.com",
  "phoneNumber": "+1234567892",
  "dateOfBirth": "1990-05-15T00:00:00",
  "address": "456 Oak Ave"
}
```

### 5. Book an Appointment

```bash
POST /api/appointments
Content-Type: application/json

{
  "doctorId": 1,
  "hospitalId": 1,
  "patientId": 1,
  "appointmentDate": "2025-11-05",
  "notes": "Regular checkup"
}
```

**Response**:
```json
{
  "id": 1,
  "doctor": {
    "id": 1,
    "name": "Dr. John Smith",
  "specialization": "Cardiology"
  },
  "hospital": {
    "id": 1,
    "name": "City General Hospital",
    "city": "New York"
  },
  "patient": {
    "id": 1,
    "name": "Jane Doe",
    "email": "jane.doe@example.com",
    "phoneNumber": "+1234567892"
  },
  "appointmentDate": "2025-11-05",
  "serialNumber": 1,
  "status": 1,
  "notes": "Regular checkup",
  "createdAt": "2025-11-01T10:30:00Z"
}
```

## ?? Concurrency Handling

### How It Works

When multiple requests try to book appointments simultaneously:

1. **Lock Acquisition**: The system acquires a row-level lock on the `doctor_hospitals` record
2. **Count Check**: Counts existing appointments (excluding cancelled) for the day
3. **Limit Validation**: Rejects if daily limit is reached
4. **Serial Assignment**: Calculates next serial number (`MAX(serial) + 1`)
5. **Atomic Insert**: Creates appointment with the serial number
6. **Transaction Commit**: Releases the lock

### Race Condition Prevention

- Uses PostgreSQL `FOR UPDATE` to lock rows
- Transaction isolation ensures consistency
- Unique constraints prevent duplicate serials
- Retry logic handles transient failures

## ?? Testing Concurrency

You can test concurrent bookings using tools like Apache JMeter, k6, or simple scripts:

```bash
# Install Apache Bench (if not installed)
# Windows: via Apache HTTP Server
# Mac: pre-installed
# Linux: sudo apt-get install apache2-utils

# Send 50 concurrent requests
ab -n 50 -c 50 -p appointment.json -T application/json https://localhost:5001/api/appointments
```

**Expected Behavior**:
- First N requests (up to daily limit) succeed with sequential serials
- Remaining requests receive 409 Conflict with "Daily limit reached" message

## ?? Error Responses

| Status Code | Scenario |
|-------------|----------|
| 200 OK | Successful GET request |
| 201 Created | Resource created successfully |
| 400 Bad Request | Invalid request data |
| 404 Not Found | Resource not found |
| 409 Conflict | Daily limit reached or duplicate booking |
| 500 Internal Server Error | Unexpected server error |

## ??? Configuration

### Application Settings

- **Connection String**: Configure in `appsettings.json`
- **Logging Levels**: Adjust in `appsettings.json`
- **CORS Policy**: Modify in `Program.cs` (currently allows all origins)

### Environment-Specific Settings

- **Development**: `appsettings.Development.json`
- **Production**: `appsettings.json` or environment variables

## ?? NuGet Packages

### Core Layer
- No external dependencies (pure domain model)

### Infrastructure Layer
- `Npgsql.EntityFrameworkCore.PostgreSQL` (9.0.4)
- `Microsoft.EntityFrameworkCore.Design` (9.0.10)

### API Layer
- `Swashbuckle.AspNetCore` (7.2.0)
- `Microsoft.EntityFrameworkCore` (9.0.10)
- `Microsoft.EntityFrameworkCore.Design` (9.0.10)
- `Npgsql.EntityFrameworkCore.PostgreSQL` (9.0.4)

## ?? Performance Considerations

- **Connection Pooling**: Npgsql automatically pools connections
- **Async/Await**: All database operations are asynchronous
- **Indexing**: Strategic indexes on frequently queried columns
- **Transaction Scope**: Transactions are kept as short as possible

## ?? Future Enhancements

- [ ] Appointment cancellation endpoint
- [ ] Appointment status update (Completed, No-Show)
- [ ] Doctor availability/schedule management
- [ ] Notification system (email/SMS)
- [ ] Authentication & Authorization (JWT)
- [ ] Rate limiting
- [ ] Caching layer (Redis)
- [ ] Admin dashboard

## ?? License

This project is for educational/demonstration purposes.

## ?? Contributing

Contributions are welcome! Please ensure:
- Code follows existing patterns
- All tests pass
- Documentation is updated

## ?? Support

For issues or questions, please open an issue in the repository.

---

**Built with ?? using .NET 9, PostgreSQL, and EF Core**
