using Bogus;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorAppointmentSystem.ConsoleApp;

public class DataSeeder
{
    private readonly AppDbContext _context;
    private readonly Random _random = new();

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

        await SeedHospitalsAndDoctorsAsync();
        await SeedPatientsAsync();

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

    private async Task SeedHospitalsAndDoctorsAsync()
    {
        const int hospitalCount = 500;
        const int minDoctorsPerHospital = 30;
        const int maxDoctorsPerHospital = 50;

        Console.WriteLine($"Creating {hospitalCount} hospitals with {minDoctorsPerHospital}-{maxDoctorsPerHospital} doctors each...");
        Console.WriteLine();

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

        // Create Bogus faker for hospitals
        var hospitalFaker = new Faker<Hospital>()
            .RuleFor(h => h.Name, f => $"{f.Company.CompanyName()} {f.PickRandom("Hospital", "Medical Center", "Clinic", "Health Center")}")
            .RuleFor(h => h.Address, f => f.Address.FullAddress())
            .RuleFor(h => h.City, f => f.Address.City())
            .RuleFor(h => h.PhoneNumber, f => f.Phone.PhoneNumber("###-###-####"))
            .RuleFor(h => h.CreatedAt, f => DateTime.UtcNow);

        // Create Bogus faker for doctors
        var doctorFaker = new Faker<Doctor>()
            .RuleFor(d => d.Name, f => f.Name.FullName())
            .RuleFor(d => d.Specialization, f => f.PickRandom(specializations))
            .RuleFor(d => d.Email, (f, d) => f.Internet.Email(d.Name).ToLower())
            .RuleFor(d => d.PhoneNumber, f => f.Phone.PhoneNumber("###-###-####"))
            .RuleFor(d => d.CreatedAt, f => DateTime.UtcNow);

        var allDoctors = new List<Doctor>();
        var doctorHospitals = new List<DoctorHospital>();

        // Generate hospitals and doctors in batches for better performance
        const int batchSize = 50;
        int totalDoctorsCreated = 0;

        for (int i = 0; i < hospitalCount; i += batchSize)
        {
            var batchHospitals = hospitalFaker.Generate(Math.Min(batchSize, hospitalCount - i));

            foreach (var hospital in batchHospitals)
            {
                int doctorCount = _random.Next(minDoctorsPerHospital, maxDoctorsPerHospital + 1);
                var batchDoctors = doctorFaker.Generate(doctorCount);

                // Ensure unique emails for doctors
                for (int j = 0; j < batchDoctors.Count; j++)
                {
                    // Add timestamp and random number to ensure uniqueness
                    var baseEmail = batchDoctors[j].Email.Split('@')[0];
                    var domain = batchDoctors[j].Email.Split('@')[1];
                    batchDoctors[j].Email = $"{baseEmail}.{DateTime.UtcNow.Ticks}.{_random.Next(1000, 9999)}@{domain}";
                }

                allDoctors.AddRange(batchDoctors);
                totalDoctorsCreated += batchDoctors.Count;

                // We'll create DoctorHospital associations after saving doctors and hospitals
                Console.Write($"\rProgress: {i + batchHospitals.Count}/{hospitalCount} hospitals, {totalDoctorsCreated} doctors prepared...");
            }

            // Save hospitals in batches
            await _context.Hospitals.AddRangeAsync(batchHospitals);
            await _context.SaveChangesAsync();
        }

        Console.WriteLine();
        Console.WriteLine($"✓ {hospitalCount} hospitals created.");

        // Save all doctors in batches
        Console.WriteLine($"Saving {allDoctors.Count} doctors...");
        for (int i = 0; i < allDoctors.Count; i += 1000)
        {
            var batch = allDoctors.Skip(i).Take(1000).ToList();
            await _context.Doctors.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
            Console.Write($"\rProgress: {Math.Min(i + 1000, allDoctors.Count)}/{allDoctors.Count} doctors saved...");
        }
        Console.WriteLine();
        Console.WriteLine($"✓ {allDoctors.Count} doctors created.");

        // Now create DoctorHospital associations
        Console.WriteLine("Creating doctor-hospital associations...");
        var hospitals = await _context.Hospitals.ToListAsync();
        var doctors = await _context.Doctors.ToListAsync();

        int doctorIndex = 0;
        foreach (var hospital in hospitals)
        {
            int doctorCount = _random.Next(minDoctorsPerHospital, maxDoctorsPerHospital + 1);

            for (int i = 0; i < doctorCount && doctorIndex < doctors.Count; i++, doctorIndex++)
            {
                var doctorHospital = new DoctorHospital
                {
                    DoctorId = doctors[doctorIndex].Id,
                    HospitalId = hospital.Id,
                    DailyPatientLimit = _random.Next(30, 71), // Random limit between 10 and 50
                    CreatedAt = DateTime.UtcNow
                };

                doctorHospitals.Add(doctorHospital);
            }

            if (doctorHospitals.Count >= 1000)
            {
                await _context.DoctorHospitals.AddRangeAsync(doctorHospitals);
                await _context.SaveChangesAsync();
                Console.Write($"\rProgress: {doctorIndex}/{doctors.Count} associations created...");
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
        Console.WriteLine($"✓ {doctorIndex} doctor-hospital associations created.");
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
                // Add timestamp and random number to ensure uniqueness
                var baseEmail = batchPatients[j].Email.Split('@')[0];
                var domain = batchPatients[j].Email.Split('@')[1];
                batchPatients[j].Email = $"{baseEmail}.{DateTime.UtcNow.Ticks}.{_random.Next(1000, 9999)}@{domain}";
            }

            allPatients.AddRange(batchPatients);
            Console.Write($"\rProgress: {i + batchCount:N0}/{patientCount:N0} patients prepared...");
        }

        Console.WriteLine();
        Console.WriteLine("Saving patients to database...");

        try
        {
            for (int i = 0; i < allPatients.Count; i += saveBatchSize)
            {
                var batch = allPatients.Skip(i).Take(saveBatchSize).ToList();
                await _context.Patients.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
                Console.Write($"\rProgress: {Math.Min(i + saveBatchSize, allPatients.Count):N0}/{allPatients.Count:N0} patients saved...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: {0}", ex.Message);
            throw;
        }
        // Save patients in batches

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

    }
}
