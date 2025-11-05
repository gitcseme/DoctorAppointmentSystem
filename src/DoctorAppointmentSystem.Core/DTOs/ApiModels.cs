namespace DoctorAppointmentSystem.Core.DTOs;

public record CreateAppointmentRequest(
    int DoctorId,
    int HospitalId,
    int PatientId,
    DateOnly AppointmentDate,
    string? Notes
);

public record AppointmentResponse(
    int Id,
    int DoctorId,
    string DoctorName,
    int HospitalId,
    string HospitalName,
    int PatientId,
    string PatientName,
    DateOnly AppointmentDate,
    int SerialNumber,
    string Status,
    string? Notes,
    DateTime CreatedAt
);

public record CreateDoctorRequest(
    string Name,
    string Specialization,
    string Email,
    string PhoneNumber
);

public record DoctorResponse(
    int Id,
    string Name,
    string Specialization,
    string Email,
    string PhoneNumber
);

public record CreateHospitalRequest(
    string Name,
    string Address,
    string City,
    string PhoneNumber
);

public record HospitalResponse(
    int Id,
    string Name,
    string Address,
    string City,
    string PhoneNumber
);

public record AssignDoctorToHospitalRequest(
    int DoctorId,
    int HospitalId,
    int DailyPatientLimit
);

public record CreatePatientRequest(
    string Name,
    string Email,
    string PhoneNumber,
    DateTime DateOfBirth,
    string Address
);

public record PatientResponse(
    int Id,
    string Name,
    string Email,
    string PhoneNumber,
    DateTime DateOfBirth,
    string Address
);
