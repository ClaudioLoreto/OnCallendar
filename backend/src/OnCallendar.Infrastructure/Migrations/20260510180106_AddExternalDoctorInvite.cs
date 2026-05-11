using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnCallendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalDoctorInvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ExternalDoctors",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteSentAtUtc",
                table: "ExternalDoctors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteToken",
                table: "ExternalDoctors",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedUserId",
                table: "ExternalDoctors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegisteredAtUtc",
                table: "ExternalDoctors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDoctors_InviteToken",
                table: "ExternalDoctors",
                column: "InviteToken",
                unique: true,
                filter: "\"InviteToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalDoctors_InviteToken",
                table: "ExternalDoctors");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ExternalDoctors");

            migrationBuilder.DropColumn(
                name: "InviteSentAtUtc",
                table: "ExternalDoctors");

            migrationBuilder.DropColumn(
                name: "InviteToken",
                table: "ExternalDoctors");

            migrationBuilder.DropColumn(
                name: "LinkedUserId",
                table: "ExternalDoctors");

            migrationBuilder.DropColumn(
                name: "RegisteredAtUtc",
                table: "ExternalDoctors");
        }
    }
}
