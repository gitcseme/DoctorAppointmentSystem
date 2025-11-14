using Bogus;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorAppointmentSystem.ConsoleApp;

public class DataSeeder
{
    private readonly AppDbContext _context;
    private readonly Random _random = new();

    private const int HospitalsToCreate = 10;
    private const int DoctorsPerHospital = 50;

    public DataSeeder(AppDbContext context)
    {
        _context = context;
    }

    public async Task SeedDataAsync()
    {
        Console.WriteLine("Starting data seeding...");
        Console.WriteLine();

        // Check if data already exists
        var existingHospitals = await _context.Hospitals.CountAsync();
        if (existingHospitals > 0)
        {
            Console.WriteLine($"Database already contains {existingHospitals} hospitals.");
            Console.Write("Do you want to clear existing data and reseed? (y/n): ");
            var response = Console.ReadLine()?.ToLower();

            if (response != "y")
            {
                Console.WriteLine("Seeding cancelled.");
                return;
            }

            await ClearDataAsync();
        }

        await SeedHospitalsAsync();
        await SeedDoctorsAsync();
        await SeedDoctorHospitalAssociationsAsync();
        await SeedPatientsAsync();

        // Postgres row-level locking needs appointment counters to be seeded
        // await SeedAppointmentCountersAsync();

        Console.WriteLine();
        Console.WriteLine("✓ Data seeding completed successfully!");
    }

    private async Task ClearDataAsync()
    {
        Console.WriteLine("Clearing existing data...");

        // Delete in correct order to respect foreign key constraints
        _context.Appointments.RemoveRange(_context.Appointments);
        _context.AppointmentCounters.RemoveRange(_context.AppointmentCounters);
        _context.DoctorHospitals.RemoveRange(_context.DoctorHospitals);
        _context.Doctors.RemoveRange(_context.Doctors);
        _context.Hospitals.RemoveRange(_context.Hospitals);
        _context.Patients.RemoveRange(_context.Patients);

        await _context.SaveChangesAsync();
        Console.WriteLine("✓ Existing data cleared.");
        Console.WriteLine();
    }

    private async Task SeedHospitalsAsync()
    {
        Console.WriteLine($"Creating {HospitalsToCreate} hospitals...");

        // Create Bogus faker for hospitals
        var hospitalFaker = new Faker<Hospital>()
            .RuleFor(h => h.Name, f => $"{f.Company.CompanyName()} {f.PickRandom("Hospital", "Medical Center", "Clinic", "Health Center")}")
            .RuleFor(h => h.Address, f => f.Address.FullAddress())
            .RuleFor(h => h.City, f => f.Address.City())
            .RuleFor(h => h.PhoneNumber, f => f.Phone.PhoneNumber("###-###-####"))
            .RuleFor(h => h.CreatedAt, f => DateTime.UtcNow);

        var hospitals = hospitalFaker.Generate(HospitalsToCreate);

        // Save hospitals in batches
        await _context.Hospitals.AddRangeAsync(hospitals);
        await _context.SaveChangesAsync();

        Console.WriteLine($"✓ {hospitals.Count} hospitals created.");
    }

    private async Task SeedDoctorsAsync()
    {
        const int totalDoctors = HospitalsToCreate * DoctorsPerHospital; // 500

        Console.WriteLine();
        Console.WriteLine($"Creating {totalDoctors:N0} doctors ({DoctorsPerHospital} per hospital)...");

        // Medical specializations
        var specializations = new[]
        {
            "Cardiology", "Dermatology", "Endocrinology", "Gastroenterology",
            "Hematology", "Nephrology", "Neurology", "Oncology",
            "Ophthalmology", "Orthopedics", "Otolaryngology", "Pediatrics",
            "Psychiatry", "Pulmonology", "Radiology", "Rheumatology",
            "Surgery", "Urology", "Anesthesiology", "Emergency Medicine",
            "Family Medicine", "Internal Medicine", "Obstetrics", "Pathology"
        };

        // Create Bogus faker for doctors
        var doctorFaker = new Faker<Doctor>()
            .RuleFor(d => d.Name, f => f.Name.FullName())
            .RuleFor(d => d.Specialization, f => f.PickRandom(specializations))
            .RuleFor(d => d.Email, (f, d) => f.Internet.Email(d.Name).ToLower())
            .RuleFor(d => d.PhoneNumber, f => f.Phone.PhoneNumber("###-###-####"))
            .RuleFor(d => d.CreatedAt, f => DateTime.UtcNow);

        var doctors = doctorFaker.Generate(totalDoctors);
        await _context.Doctors.AddRangeAsync(doctors);
        await _context.SaveChangesAsync();

        Console.WriteLine($"✓ {doctors.Count:N0} doctors created.");
    }

    private async Task SeedDoctorHospitalAssociationsAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Creating doctor-hospital associations...");

        var hospitals = await _context.Hospitals.OrderBy(h => h.Id).ToListAsync();
        var doctors = await _context.Doctors.OrderBy(d => d.Id).ToListAsync();

        var doctorHospitals = new List<DoctorHospital>();
        int doctorIndex = 0;

        foreach (var hospital in hospitals)
        {
            // Assign exactly 50 doctors to each hospital
            for (int i = 0; i < DoctorsPerHospital && doctorIndex < doctors.Count; i++, doctorIndex++)
            {
                var doctorHospital = new DoctorHospital
                {
                    DoctorId = doctors[doctorIndex].Id,
                    HospitalId = hospital.Id,
                    DailyPatientLimit = 50, // Fixed daily limit for predictability
                    CreatedAt = DateTime.UtcNow
                };

                doctorHospitals.Add(doctorHospital);
            }

            // Save in batches of 1000
            if (doctorHospitals.Count >= 1000)
            {
                await _context.DoctorHospitals.AddRangeAsync(doctorHospitals);
                await _context.SaveChangesAsync();
                Console.Write($"\rProgress: {doctorIndex:N0}/{doctors.Count:N0} associations created...");
                doctorHospitals.Clear();
            }
        }

        // Save remaining associations
        if (doctorHospitals.Any())
        {
            await _context.DoctorHospitals.AddRangeAsync(doctorHospitals);
            await _context.SaveChangesAsync();
        }

        Console.WriteLine();
        Console.WriteLine($"✓ {doctorIndex:N0} doctor-hospital associations created.");
    }

    private async Task SeedPatientsAsync()
    {
        const int patientCount = 100000;
        Console.WriteLine();
        Console.WriteLine($"Creating {patientCount:N0} unique patients...");
        Console.WriteLine();

        // Create Bogus faker for patients
        var patientFaker = new Faker<Patient>()
            .RuleFor(p => p.Name, f => f.Name.FullName())
            .RuleFor(p => p.Email, (f, p) => f.Internet.Email(p.Name).ToLower())
            .RuleFor(p => p.PhoneNumber, f => f.Phone.PhoneNumber("###-###-####"))
            .RuleFor(p => p.DateOfBirth, f => f.Date.Past(80, DateTime.UtcNow.AddYears(-18))) // 18-98 years old
            .RuleFor(p => p.Address, f => f.Address.FullAddress())
            .RuleFor(p => p.CreatedAt, f => DateTime.UtcNow);

        var allPatients = new List<Patient>();

        // Generate patients in batches for better performance
        const int generateBatchSize = 10000;
        const int saveBatchSize = 5000;

        for (int i = 0; i < patientCount; i += generateBatchSize)
        {
            var batchCount = Math.Min(generateBatchSize, patientCount - i);
            var batchPatients = patientFaker.Generate(batchCount);

            // Ensure unique emails for patients
            for (int j = 0; j < batchPatients.Count; j++)
            {
                var baseEmail = batchPatients[j].Email.Split('@')[0];
                var domain = batchPatients[j].Email.Split('@')[1];
                batchPatients[j].Email = $"{baseEmail}.{DateTime.UtcNow.Ticks}.{_random.Next(1000, 9999)}@{domain}";
            }

            allPatients.AddRange(batchPatients);
            Console.Write($"\rProgress: {i + batchCount:N0}/{patientCount:N0} patients prepared...");
        }

        Console.WriteLine();
        Console.WriteLine("Saving patients to database...");

        for (int i = 0; i < allPatients.Count; i += saveBatchSize)
        {
            var batch = allPatients.Skip(i).Take(saveBatchSize).ToList();
            await _context.Patients.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
            Console.Write($"\rProgress: {Math.Min(i + saveBatchSize, allPatients.Count):N0}/{allPatients.Count:N0} patients saved...");
        }

        Console.WriteLine();
        Console.WriteLine($"✓ {allPatients.Count:N0} unique patients created.");
    }

    public async Task DisplayStatisticsAsync()
    {
        Console.WriteLine();
        Console.WriteLine("=== Database Statistics ===");
        Console.WriteLine($"Total Hospitals: {await _context.Hospitals.CountAsync()}");
        Console.WriteLine($"Total Doctors: {await _context.Doctors.CountAsync()}");
        Console.WriteLine($"Total Doctor-Hospital Associations: {await _context.DoctorHospitals.CountAsync()}");
        Console.WriteLine($"Total Patients: {await _context.Patients.CountAsync()}");
        Console.WriteLine();
        Console.WriteLine("=== Predictable Configuration ===");
        Console.WriteLine($"Doctors per Hospital: {DoctorsPerHospital} (fixed)");
        Console.WriteLine("Daily Patient Limit: 50 (fixed)");
        Console.WriteLine();
    }

    private async Task SeedAppointmentCountersAsync()
    {
        Console.WriteLine("\nSeeding Appointment Counters For Tomorrow");
        var doctorHospitals = await _context.DoctorHospitals.ToListAsync();
        var appointmentCounters = new List<AppointmentCounter>();
        foreach (var dh in doctorHospitals)
        {
            var counter = new AppointmentCounter
            {
                DoctorHospitalId = dh.Id,
                AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                CurrentSerial = 0,
                AppointmentCount = 0,
                UpdatedAt = DateTime.UtcNow
            };
            appointmentCounters.Add(counter);
        }
        await _context.AppointmentCounters.AddRangeAsync(appointmentCounters);
        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ {appointmentCounters.Count} AppointmentCounters created.");
    }
}
