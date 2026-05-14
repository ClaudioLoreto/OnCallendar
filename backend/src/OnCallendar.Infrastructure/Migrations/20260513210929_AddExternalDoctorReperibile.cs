using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnCallendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalDoctorReperibile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExternalDoctorReperibileId",
                table: "Shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ExternalDoctorReperibileId",
                table: "Shifts",
                column: "ExternalDoctorReperibileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_ExternalDoctors_ExternalDoctorReperibileId",
                table: "Shifts",
                column: "ExternalDoctorReperibileId",
                principalTable: "ExternalDoctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_ExternalDoctors_ExternalDoctorReperibileId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_ExternalDoctorReperibileId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ExternalDoctorReperibileId",
                table: "Shifts");
        }
    }
}
