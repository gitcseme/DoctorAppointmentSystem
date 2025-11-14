var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("appointments-db")
    //.WithDataVolume()
    .WithPgAdmin();

var redis = builder.AddRedis("redis")
    //.WithDataVolume()
    .WithRedisInsight();

builder.AddProject<Projects.DoctorAppointmentSystem_Api>("doctorappointmentsystem-api")
    .WithReference(postgres)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(redis);

builder.Build().Run();
