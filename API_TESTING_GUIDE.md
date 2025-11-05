# API Testing Examples
# Use these examples with tools like Postman, REST Client, or curl

## Base URL
```
https://localhost:5001
```

---

## 1. Health Check

### Request
```http
GET /health HTTP/1.1
Host: localhost:5001
```

### Expected Response
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-01T10:00:00Z",
  "database": "Connected"
}
```

---

## 2. Create Doctor

### Request
```http
POST /api/doctors HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "name": "Dr. Sarah Johnson",
  "specialization": "Cardiology",
  "email": "sarah.johnson@hospital.com",
  "phoneNumber": "+1-555-0101"
}
```

### Expected Response (201 Created)
```json
{
  "id": 1,
  "name": "Dr. Sarah Johnson",
  "specialization": "Cardiology",
  "email": "sarah.johnson@hospital.com",
  "phoneNumber": "+1-555-0101"
}
```

---

## 3. Create Hospital

### Request
```http
POST /api/hospitals HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "name": "Central Medical Center",
  "address": "789 Healthcare Blvd",
  "city": "Boston",
  "phoneNumber": "+1-555-0200"
}
```

### Expected Response (201 Created)
```json
{
  "id": 1,
  "name": "Central Medical Center",
  "address": "789 Healthcare Blvd",
  "city": "Boston",
  "phoneNumber": "+1-555-0200"
}
```

---

## 4. Assign Doctor to Hospital

### Request
```http
POST /api/doctors/assign-to-hospital HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "doctorId": 1,
  "hospitalId": 1,
  "dailyPatientLimit": 25
}
```

### Expected Response (200 OK)
```json
{
  "message": "Doctor successfully assigned to hospital."
}
```

---

## 5. Create Patient

### Request
```http
POST /api/patients HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "name": "Michael Brown",
  "email": "michael.brown@email.com",
  "phoneNumber": "+1-555-0301",
  "dateOfBirth": "1985-03-15T00:00:00",
  "address": "456 Elm Street, Apt 3B"
}
```

### Expected Response (201 Created)
```json
{
  "id": 1,
  "name": "Michael Brown",
  "email": "michael.brown@email.com",
  "phoneNumber": "+1-555-0301",
  "dateOfBirth": "1985-03-15T00:00:00",
  "address": "456 Elm Street, Apt 3B"
}
```

---

## 6. Book First Appointment

### Request
```http
POST /api/appointments HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "doctorId": 1,
  "hospitalId": 1,
  "patientId": 1,
  "appointmentDate": "2025-11-10",
  "notes": "Annual checkup"
}
```

### Expected Response (201 Created)
```json
{
  "id": 1,
  "doctor": {
    "id": 1,
    "name": "Dr. Sarah Johnson",
    "specialization": "Cardiology"
  },
  "hospital": {
    "id": 1,
    "name": "Central Medical Center",
    "city": "Boston"
  },
  "patient": {
    "id": 1,
 "name": "Michael Brown",
    "email": "michael.brown@email.com",
    "phoneNumber": "+1-555-0301"
  },
  "appointmentDate": "2025-11-10",
  "serialNumber": 1,
  "status": 1,
  "notes": "Annual checkup",
  "createdAt": "2025-11-01T10:30:00Z"
}
```

---

## 7. Book Second Appointment (Same Doctor, Hospital, Date)

### Request
```http
POST /api/appointments HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "doctorId": 1,
  "hospitalId": 1,
  "patientId": 2,
  "appointmentDate": "2025-11-10",
  "notes": "Follow-up consultation"
}
```

### Expected Response (201 Created)
```json
{
  "id": 2,
  "serialNumber": 2,
  ...
}
```
**Note:** Serial number automatically increments to 2

---

## 8. Get Appointments by Doctor, Hospital, and Date

### Request
```http
GET /api/appointments?doctorId=1&hospitalId=1&date=2025-11-10 HTTP/1.1
Host: localhost:5001
```

### Expected Response (200 OK)
```json
[
  {
    "id": 1,
    "serialNumber": 1,
    "appointmentDate": "2025-11-10",
    "status": 1,
    "notes": "Annual checkup",
    "patient": {
   "id": 1,
      "name": "Michael Brown",
 "phoneNumber": "+1-555-0301"
    },
    "createdAt": "2025-11-01T10:30:00Z"
  },
  {
    "id": 2,
  "serialNumber": 2,
    "appointmentDate": "2025-11-10",
    "status": 1,
    "notes": "Follow-up consultation",
    "patient": {
 "id": 2,
      "name": "Jane Doe",
"phoneNumber": "+1-555-0302"
    },
    "createdAt": "2025-11-01T10:35:00Z"
  }
]
```

---

## 9. Get Specific Appointment

### Request
```http
GET /api/appointments/1 HTTP/1.1
Host: localhost:5001
```

### Expected Response (200 OK)
```json
{
  "id": 1,
  "doctor": {
  "id": 1,
"name": "Dr. Sarah Johnson",
    "specialization": "Cardiology"
  },
  "hospital": {
    "id": 1,
    "name": "Central Medical Center",
    "city": "Boston"
  },
  "patient": {
    "id": 1,
    "name": "Michael Brown",
    "email": "michael.brown@email.com",
    "phoneNumber": "+1-555-0301"
  },
  "appointmentDate": "2025-11-10",
  "serialNumber": 1,
  "status": 1,
  "notes": "Annual checkup",
  "createdAt": "2025-11-01T10:30:00Z"
}
```

---

## 10. Error Scenarios

### Daily Limit Reached

**Request:** Try to book appointment beyond daily limit (e.g., 26th when limit is 25)

**Expected Response (409 Conflict)**
```json
{
  "statusCode": 409,
  "message": "Daily patient limit (25) reached for this doctor at this hospital on 2025-11-10.",
  "timestamp": "2025-11-01T11:00:00Z"
}
```

### Doctor Not Found

**Request:** Book appointment with invalid doctorId

**Expected Response (404 Not Found)**
```json
{
  "message": "Doctor with ID 999 is not associated with Hospital ID 1."
}
```

### Duplicate Booking

**Request:** Same patient tries to book with same doctor/hospital/date twice

**Expected Response (Constraint Violation)**
Database constraint prevents duplicate bookings.

---

## Testing Concurrency

### Using curl (Linux/Mac/Git Bash)
```bash
# Create appointment.json file
cat > appointment.json << EOF
{
  "doctorId": 1,
  "hospitalId": 1,
  "patientId": 1,
  "appointmentDate": "2025-11-15",
  "notes": "Test appointment"
}
EOF

# Send 10 concurrent requests
for i in {1..10}; do
  curl -X POST https://localhost:5001/api/appointments \
    -H "Content-Type: application/json" \
    -d @appointment.json \
    --insecure &
done
wait
```

### Using PowerShell
```powershell
$body = @{
  doctorId = 1
  hospitalId = 1
  patientId = 1
  appointmentDate = "2025-11-15"
  notes = "Test appointment"
} | ConvertTo-Json

$jobs = 1..10 | ForEach-Object {
  Start-Job -ScriptBlock {
    param($body)
  Invoke-RestMethod -Method Post `
   -Uri "https://localhost:5001/api/appointments" `
      -Body $body `
      -ContentType "application/json" `
      -SkipCertificateCheck
  } -ArgumentList $body
}

$jobs | Wait-Job | Receive-Job
```

### Expected Result
- All requests processed safely
- Sequential serial numbers assigned (1, 2, 3, ...)
- No duplicate serial numbers
- All requests succeed (within daily limit)

---

## Notes

1. **HTTPS Certificate**: The API uses a development certificate. In tools like Postman, you may need to disable SSL verification or install the dev certificate.

2. **Date Format**: Use ISO 8601 format: `YYYY-MM-DD` for dates.

3. **Status Codes**:
   - `1` = Scheduled
   - `2` = Completed
- `3` = Cancelled
   - `4` = NoShow

4. **Serial Numbers**: Automatically assigned by the system. Cannot be manually set.

5. **Timestamps**: All timestamps are in UTC.
