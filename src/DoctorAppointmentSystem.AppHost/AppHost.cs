var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("appointments-db")
    //.WithDataVolume()
    .WithPgAdmin();

var redis = builder.AddRedis("redis")
    //.WithDataVolume()
    .WithRedisInsight();

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin(); // Adds RabbitMQ management UI

builder.AddProject<Projects.DoctorAppointmentSystem_Api>("doctorappointmentsystem-api")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(rabbitmq);

builder.Build().Run();
