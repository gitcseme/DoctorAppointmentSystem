using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Doctor> Doctors { get; set; }
    public DbSet<Hospital> Hospitals { get; set; }
    public DbSet<DoctorHospital> DoctorHospitals { get; set; }
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<AppointmentCounter> AppointmentCounters { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.ToTable("doctors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Specialization).HasColumnName("specialization").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Hospital>(entity =>
        {
            entity.ToTable("hospitals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(500).IsRequired();
            entity.Property(e => e.City).HasColumnName("city").HasMaxLength(100).IsRequired();
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<DoctorHospital>(entity =>
        {
            entity.ToTable("doctor_hospitals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DoctorId).HasColumnName("doctor_id");
            entity.Property(e => e.HospitalId).HasColumnName("hospital_id");
            entity.Property(e => e.DailyPatientLimit).HasColumnName("daily_patient_limit").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Doctor)
                .WithMany(d => d.DoctorHospitals)
                .HasForeignKey(e => e.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Hospital)
                .WithMany(h => h.DoctorHospitals)
                .HasForeignKey(e => e.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.DoctorId, e.HospitalId }).IsUnique();
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("patients");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth").IsRequired();
            entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(500).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable("appointments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DoctorHospitalId).HasColumnName("doctor_hospital_id");
            entity.Property(e => e.PatientId).HasColumnName("patient_id");
            entity.Property(e => e.AppointmentDate).HasColumnName("appointment_date").IsRequired();
            entity.Property(e => e.SerialNumber).HasColumnName("serial_number").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.DoctorHospital)
                .WithMany(dh => dh.Appointments)
                .HasForeignKey(e => e.DoctorHospitalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: one patient can have only one appointment per doctor per hospital per date
            entity.HasIndex(e => new { e.DoctorHospitalId, e.PatientId, e.AppointmentDate }).IsUnique();

            // Unique constraint: serial number must be unique per doctor per hospital per date
            entity.HasIndex(e => new { e.DoctorHospitalId, e.AppointmentDate, e.SerialNumber }).IsUnique();
        });

        modelBuilder.Entity<AppointmentCounter>(entity =>
        {
            entity.ToTable("appointment_counters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DoctorHospitalId).HasColumnName("doctor_hospital_id");
            entity.Property(e => e.AppointmentDate).HasColumnName("appointment_date").IsRequired();
            entity.Property(e => e.CurrentSerial).HasColumnName("current_serial").HasDefaultValue(0).IsRequired();
            entity.Property(e => e.AppointmentCount).HasColumnName("appointment_count").HasDefaultValue(0).IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.DoctorHospital)
                .WithMany()
                .HasForeignKey(e => e.DoctorHospitalId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: one counter per doctor-hospital-date combination
            entity.HasIndex(e => new { e.DoctorHospitalId, e.AppointmentDate }).IsUnique();
        });
    }
}
