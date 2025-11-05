# Doctor Appointment System - Quick Setup Script
# Run this script from the solution root directory

Write-Host "=== Doctor Appointment System - Quick Setup ===" -ForegroundColor Cyan
Write-Host ""

# Check if PostgreSQL is running
Write-Host "1. Checking PostgreSQL connection..." -ForegroundColor Yellow
try {
    $env:PGPASSWORD = "your_password"
    $result = psql -U postgres -c "SELECT version();" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? PostgreSQL is running" -ForegroundColor Green
    } else {
        Write-Host "? PostgreSQL connection failed. Please ensure PostgreSQL is installed and running." -ForegroundColor Red
        Write-Host "  Update the password in this script and appsettings.json" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "? PostgreSQL not found or not accessible" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2. Creating database..." -ForegroundColor Yellow
$createDb = @"
SELECT 'CREATE DATABASE doctor_appointment_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'doctor_appointment_db')\gexec
"@

try {
    $createDb | psql -U postgres 2>&1 | Out-Null
    Write-Host "? Database 'doctor_appointment_db' is ready" -ForegroundColor Green
} catch {
    Write-Host "? Database might already exist or creation failed" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "3. Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -eq 0) {
    Write-Host "? Packages restored" -ForegroundColor Green
} else {
    Write-Host "? Package restore failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "4. Building solution..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -eq 0) {
    Write-Host "? Build successful" -ForegroundColor Green
} else {
  Write-Host "? Build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "5. Applying database migrations..." -ForegroundColor Yellow
Set-Location src\DoctorAppointmentSystem.Api
dotnet ef database update --project ..\DoctorAppointmentSystem.Infrastructure\DoctorAppointmentSystem.Infrastructure.csproj
if ($LASTEXITCODE -eq 0) {
    Write-Host "? Database schema created" -ForegroundColor Green
} else {
    Write-Host "? Migration failed" -ForegroundColor Red
    Set-Location ..\..
    exit 1
}
Set-Location ..\..

Write-Host ""
Write-Host "=== Setup Complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "To run the application:" -ForegroundColor Cyan
Write-Host "  cd src\DoctorAppointmentSystem.Api" -ForegroundColor White
Write-Host "  dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "Then open your browser to:" -ForegroundColor Cyan
Write-Host "  https://localhost:5001  (Swagger UI)" -ForegroundColor White
Write-Host "  http://localhost:5000 (HTTP)" -ForegroundColor White
Write-Host ""
Write-Host "Health check endpoint:" -ForegroundColor Cyan
Write-Host "  https://localhost:5001/health" -ForegroundColor White
Write-Host ""
