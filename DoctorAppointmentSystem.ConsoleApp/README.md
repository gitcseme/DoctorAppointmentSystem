# Doctor Appointment System - Data Seeding Console App

This console application uses the **Bogus** library to generate realistic test data for the Doctor Appointment System database.

## Features

- Generates **500 hospitals** with complete details (name, address, city, phone)
- Creates **30-50 doctors per hospital** (randomized)
- Assigns doctors to hospitals with random daily patient limits (10-50)
- Uses realistic fake data (names, emails, phone numbers, addresses)
- 24 medical specializations (Cardiology, Neurology, Pediatrics, etc.)
- Batch processing for optimal performance
- Progress indicators during data generation
- Database statistics display after seeding

## Prerequisites

1. **PostgreSQL** database running
2. **Database created** (e.g., `doctor_appointment_db`)
3. **Migrations applied** to the database

## Configuration

Update the connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=doctor_appointment_db;Username=your_username;Password=your_password"
  }
}
```

## How to Run

### Option 1: Using dotnet CLI

```bash
cd DoctorAppointmentSystem.ConsoleApp
dotnet run
```

### Option 2: Using Visual Studio

1. Set `DoctorAppointmentSystem.ConsoleApp` as the startup project
2. Press F5 or click "Run"

## Expected Output

```
??????????????????????????????????????????????????????????????
?     Doctor Appointment System - Data Seeding Tool          ?
??????????????????????????????????????????????????????????????

Testing database connection...
? Database connection successful!

Starting data seeding...

Creating 500 hospitals with 30-50 doctors each...

Progress: 500/500 hospitals, 20000 doctors prepared...
? 500 hospitals created.
Saving 20000 doctors...
Progress: 20000/20000 doctors saved...
? 20000 doctors created.
Creating doctor-hospital associations...
Progress: 20000/20000 associations created...
? 20000 doctor-hospital associations created.

? Data seeding completed successfully!

=== Database Statistics ===
Total Hospitals: 500
Total Doctors: 20000
Total Doctor-Hospital Associations: 20000
Total Patients: 0
Total Appointments: 0

=== Sample Hospital with Doctors ===
Hospital: Borer, Watsica and Abbott Medical Center
City: New Shawnaton
Address: 123 Main Street, Suite 456
Phone: 555-123-4567
Number of Doctors: 42

Sample Doctors:
  - Dr. John Smith (Cardiology) - Daily Limit: 35
    Email: john.smith.123456789.1234@example.com, Phone: 555-234-5678
  - Dr. Sarah Johnson (Neurology) - Daily Limit: 42
    Email: sarah.johnson.123456789.5678@example.com, Phone: 555-345-6789
  ... and 40 more doctors

Press any key to exit...
```

## Data Generated

### Hospitals (500)
- Name: Realistic company names + "Hospital", "Medical Center", "Clinic", or "Health Center"
- Address: Full addresses with street, city, state, zip
- City: Various city names
- Phone: Format: ###-###-####

### Doctors (~20,000)
- Name: Realistic full names
- Email: Generated from doctor names (unique)
- Phone: Format: ###-###-####
- Specialization: One of 24 medical specializations

### Medical Specializations
- Cardiology, Dermatology, Endocrinology, Gastroenterology
- Hematology, Nephrology, Neurology, Oncology
- Ophthalmology, Orthopedics, Otolaryngology, Pediatrics
- Psychiatry, Pulmonology, Radiology, Rheumatology
- Surgery, Urology, Anesthesiology, Emergency Medicine
- Family Medicine, Internal Medicine, Obstetrics, Pathology

### Doctor-Hospital Associations
- Each doctor is assigned to exactly one hospital
- Daily patient limit: Random between 10 and 50

## Performance

- Uses batch processing (50 hospitals at a time)
- Saves doctors in batches of 1,000
- Saves associations in batches of 1,000
- Expected total time: **1-3 minutes** depending on hardware and database performance

## Re-seeding

When you run the application and data already exists:
- The app will ask if you want to clear existing data
- Type `y` to clear and reseed
- Type `n` to cancel

**Warning:** This will delete all existing appointments, patients, doctors, and hospitals!

## Troubleshooting

### Cannot connect to database
- Verify PostgreSQL is running
- Check connection string in `appsettings.json`
- Ensure database exists
- Verify username/password

### Migration errors
Make sure you've run migrations first:
```bash
cd src/DoctorAppointmentSystem.Infrastructure
dotnet ef database update
```

### Unique constraint errors
- Clear the database before re-seeding
- The app will prompt you to clear existing data

## Next Steps

After seeding, you can:
1. Run the API project to test appointment creation
2. Query the database to verify data
3. Test concurrent appointment creation with multiple users
4. Create patients and appointments via the API endpoints

## Technical Details

- **Framework:** .NET 9
- **Fake Data Library:** Bogus 35.6.5
- **Database:** PostgreSQL with Entity Framework Core
- **Batch Size:** 50 hospitals, 1000 doctors/associations per save
- **Email Uniqueness:** Ensured with timestamp + random number suffix
