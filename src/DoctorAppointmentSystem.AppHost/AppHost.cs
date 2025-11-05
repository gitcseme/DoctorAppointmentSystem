var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("appointments-db")
    .WithDataVolume()
    .WithPgAdmin();

builder.AddProject<Projects.DoctorAppointmentSystem_Api>("doctorappointmentsystem-api")
    .WithReference(postgres);

builder.Build().Run();
