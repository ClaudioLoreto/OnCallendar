using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnCallendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalDoctors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExternalDoctorId",
                table: "Shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalDoctors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LastName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(170)", maxLength: 170, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalDoctors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalDoctors_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ExternalDoctorId",
                table: "Shifts",
                column: "ExternalDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDoctors_TenantId_LastName",
                table: "ExternalDoctors",
                columns: new[] { "TenantId", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDoctors_TenantId_NormalizedKey",
                table: "ExternalDoctors",
                columns: new[] { "TenantId", "NormalizedKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_ExternalDoctors_ExternalDoctorId",
                table: "Shifts",
                column: "ExternalDoctorId",
                principalTable: "ExternalDoctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_ExternalDoctors_ExternalDoctorId",
                table: "Shifts");

            migrationBuilder.DropTable(
                name: "ExternalDoctors");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_ExternalDoctorId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ExternalDoctorId",
                table: "Shifts");
        }
    }
}
