using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DoctorAppointmentSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentCounterTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_counters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    doctor_hospital_id = table.Column<int>(type: "integer", nullable: false),
                    appointment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    current_serial = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    appointment_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_counters", x => x.id);
                    table.ForeignKey(
                        name: "FK_appointment_counters_doctor_hospitals_doctor_hospital_id",
                        column: x => x.doctor_hospital_id,
                        principalTable: "doctor_hospitals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_counters_doctor_hospital_id_appointment_date",
                table: "appointment_counters",
                columns: new[] { "doctor_hospital_id", "appointment_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_counters");
        }
    }
}
