# Quick Reference Card

## ?? Quick Start

```bash
# 1. Update connection string in appsettings.json
# 2. Create database
psql -U postgres -c "CREATE DATABASE doctor_appointment_db;"

# 3. Apply migrations
cd src/DoctorAppointmentSystem.Api
dotnet ef database update --project ../DoctorAppointmentSystem.Infrastructure

# 4. Run
dotnet run

# 5. Open browser to https://localhost:5001
```

---

## ?? Important URLs

| URL | Purpose |
|-----|---------|
| `https://localhost:5001` | Swagger UI (Main) |
| `https://localhost:5001/health` | Health Check |
| `http://localhost:5000` | HTTP Endpoint |

---

## ?? API Endpoints Cheat Sheet

### Doctors
```
POST   /api/doctors            # Create
GET    /api/doctors           # List all
GET    /api/doctors/{id}           # Get by ID
POST   /api/doctors/assign-to-hospital        # Assign to hospital
GET/api/doctors/{doctorId}/hospitals/{hospitalId} # Get association
```

### Hospitals
```
POST   /api/hospitals       # Create
GET    /api/hospitals       # List all
GET    /api/hospitals/{id}      # Get by ID
GET    /api/hospitals/{id}/doctors # Get hospital's doctors
```

### Patients
```
POST   /api/patients      # Create
GET    /api/patients      # List all
GET    /api/patients/{id} # Get by ID
```

### Appointments
```
POST   /api/appointments  # Book appointment ?
GET    /api/appointments/{id} # Get by ID
GET    /api/appointments?doctorId={id}&hospitalId={id}&date={date} # Query
GET    /api/appointments/doctor/{doctorId}/hospital/{hospitalId}/date/{date}
```

---

## ?? Sample JSON Payloads

### Create Doctor
```json
{
  "name": "Dr. John Smith",
  "specialization": "Cardiology",
  "email": "john@hospital.com",
  "phoneNumber": "+1234567890"
}
```

### Create Hospital
```json
{
  "name": "City Hospital",
  "address": "123 Main St",
  "city": "New York",
  "phoneNumber": "+1234567891"
}
```

### Assign Doctor to Hospital
```json
{
  "doctorId": 1,
  "hospitalId": 1,
  "dailyPatientLimit": 20
}
```

### Create Patient
```json
{
  "name": "Jane Doe",
  "email": "jane@email.com",
  "phoneNumber": "+1234567892",
  "dateOfBirth": "1990-05-15T00:00:00",
  "address": "456 Oak Ave"
}
```

### Book Appointment ?
```json
{
  "doctorId": 1,
  "hospitalId": 1,
  "patientId": 1,
  "appointmentDate": "2025-11-10",
  "notes": "Regular checkup"
}
```

---

## ??? Database Commands

```bash
# Create database
psql -U postgres -c "CREATE DATABASE doctor_appointment_db;"

# Connect to database
psql -U postgres -d doctor_appointment_db

# View tables
\dt

# View table structure
\d appointments

# Drop database (careful!)
psql -U postgres -c "DROP DATABASE doctor_appointment_db;"
```

---

## ??? EF Core Migration Commands

```bash
# Add migration
dotnet ef migrations add MigrationName --project src/DoctorAppointmentSystem.Infrastructure --startup-project src/DoctorAppointmentSystem.Api

# Update database
dotnet ef database update --project src/DoctorAppointmentSystem.Infrastructure --startup-project src/DoctorAppointmentSystem.Api

# Remove last migration
dotnet ef migrations remove --project src/DoctorAppointmentSystem.Infrastructure --startup-project src/DoctorAppointmentSystem.Api

# List migrations
dotnet ef migrations list --project src/DoctorAppointmentSystem.Infrastructure --startup-project src/DoctorAppointmentSystem.Api

# Generate SQL script
dotnet ef migrations script --project src/DoctorAppointmentSystem.Infrastructure --startup-project src/DoctorAppointmentSystem.Api
```

---

## ?? Key Database Tables

```
doctors
??? id (PK)
??? name
??? specialization
??? email (unique)
??? phone_number
??? created_at

hospitals
??? id (PK)
??? name
??? address
??? city
??? phone_number
??? created_at

doctor_hospitals
??? id (PK)
??? doctor_id (FK ? doctors)
??? hospital_id (FK ? hospitals)
??? daily_patient_limit ?
??? created_at
??? UNIQUE(doctor_id, hospital_id)

patients
??? id (PK)
??? name
??? email (unique)
??? phone_number
??? date_of_birth
??? address
??? created_at

appointments ?
??? id (PK)
??? doctor_hospital_id (FK ? doctor_hospitals)
??? patient_id (FK ? patients)
??? appointment_date
??? serial_number ? (auto-assigned)
??? status (1=Scheduled, 2=Completed, 3=Cancelled, 4=NoShow)
??? notes
??? created_at
??? updated_at
??? UNIQUE(doctor_hospital_id, appointment_date, serial_number)
??? UNIQUE(doctor_hospital_id, patient_id, appointment_date)
```

---

## ?? Concurrency Control (How It Works)

```
1. Request arrives
   ?
2. BEGIN TRANSACTION
?
3. SELECT * FROM doctor_hospitals WHERE id = X FOR UPDATE ?
   (locks the row until transaction completes)
   ?
4. Count existing appointments for doctor/hospital/date
   ?
5. IF count >= daily_limit THEN reject (409 Conflict)
   ?
6. Calculate next_serial = MAX(serial_number) + 1
   ?
7. INSERT appointment with next_serial
   ?
8. COMMIT TRANSACTION
   (releases lock)
```

**Key Point:** `FOR UPDATE` ensures only one request can proceed at a time for the same doctor/hospital/date combination.

---

## ?? Common Error Codes

| Code | Meaning | Common Cause |
|------|---------|--------------|
| 200 | OK | Successful GET |
| 201 | Created | Resource created |
| 400 | Bad Request | Invalid JSON or validation error |
| 404 | Not Found | Doctor, hospital, or patient doesn't exist |
| 409 | Conflict | Daily limit reached |
| 500 | Server Error | Unexpected error (check logs) |

---

## ?? Quick Test Sequence

```bash
# 1. Create doctor
curl -X POST https://localhost:5001/api/doctors \
  -H "Content-Type: application/json" \
  -d '{"name":"Dr. Smith","specialization":"Cardiology","email":"smith@hospital.com","phoneNumber":"+1234567890"}' \
  -k

# 2. Create hospital
curl -X POST https://localhost:5001/api/hospitals \
  -H "Content-Type: application/json" \
  -d '{"name":"City Hospital","address":"123 Main","city":"NYC","phoneNumber":"+1234567891"}' \
  -k

# 3. Assign doctor to hospital (dailyPatientLimit: 20)
curl -X POST https://localhost:5001/api/doctors/assign-to-hospital \
  -H "Content-Type: application/json" \
  -d '{"doctorId":1,"hospitalId":1,"dailyPatientLimit":20}' \
  -k

# 4. Create patient
curl -X POST https://localhost:5001/api/patients \
  -H "Content-Type: application/json" \
  -d '{"name":"John Doe","email":"john@email.com","phoneNumber":"+1234567892","dateOfBirth":"1990-01-01","address":"456 Oak"}' \
  -k

# 5. Book appointment (serial number assigned automatically)
curl -X POST https://localhost:5001/api/appointments \
  -H "Content-Type: application/json" \
  -d '{"doctorId":1,"hospitalId":1,"patientId":1,"appointmentDate":"2025-11-10","notes":"Checkup"}' \
  -k

# 6. View appointments
curl https://localhost:5001/api/appointments?doctorId=1&hospitalId=1&date=2025-11-10 -k
```

---

## ?? Project Structure

```
DoctorAppointmentSystem/
??? src/
?   ??? DoctorAppointmentSystem.Core/      # Domain Layer
?   ?   ??? Entities/             # Domain models
?   ?   ??? Interfaces/   # Repository interfaces
?   ?   ??? DTOs/      # Data transfer objects
?   ?   ??? Exceptions/         # Domain exceptions
?   ?
?   ??? DoctorAppointmentSystem.Infrastructure/ # Data Layer
?   ?   ??? Data/# DbContext
?   ?   ??? Repositories/          # Repository implementations
?   ?   ??? Migrations/            # EF Core migrations
?   ?
?   ??? DoctorAppointmentSystem.Api/   # API Layer
?   ??? Controllers/      # API endpoints
?       ??? Middleware/            # Global middleware
?       ??? Program.cs             # App configuration
?    ??? appsettings.json         # Configuration
?
??? README.md         # Main documentation
??? API_TESTING_GUIDE.md            # Testing guide
??? IMPLEMENTATION_SUMMARY.md     # What was built
??? QUICK_REFERENCE.md # This file
??? setup.ps1    # Automated setup
??? .gitignore      # Git exclusions
```

---

## ?? Configuration Files

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=doctor_appointment_db;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

### Update Connection String
1. Open `src/DoctorAppointmentSystem.Api/appsettings.json`
2. Update the `Password` field
3. Save and run

---

## ?? Tips & Tricks

- **Use Swagger UI**: Easiest way to test the API interactively
- **Check Health**: `/health` endpoint verifies database connectivity
- **Date Format**: Always use `YYYY-MM-DD` for appointment dates
- **Serial Numbers**: System assigns automatically - don't try to set manually
- **Concurrency**: Test with multiple simultaneous requests to verify locking
- **Logs**: Check console output for detailed error messages

---

## ?? Troubleshooting

| Problem | Solution |
|---------|----------|
| Can't connect to database | Check PostgreSQL is running and credentials are correct |
| Migration fails | Ensure database exists and connection string is correct |
| Build errors | Run `dotnet restore` then `dotnet build` |
| Port already in use | Change port in `launchSettings.json` or kill existing process |
| SSL certificate error | Use `--insecure/-k` flag in curl or disable SSL verification |

---

**Built with .NET 9 + PostgreSQL + EF Core**
**Production-ready with atomic operations and concurrency control** ??
