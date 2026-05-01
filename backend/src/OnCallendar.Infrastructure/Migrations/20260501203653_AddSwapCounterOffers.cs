using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnCallendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSwapCounterOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SwapCounterOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SwapRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedByMedicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferedShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwapCounterOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwapCounterOffers_AspNetUsers_ProposedByMedicoId",
                        column: x => x.ProposedByMedicoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SwapCounterOffers_Shifts_OfferedShiftId",
                        column: x => x.OfferedShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SwapCounterOffers_SwapRequests_SwapRequestId",
                        column: x => x.SwapRequestId,
                        principalTable: "SwapRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SwapCounterOffers_OfferedShiftId",
                table: "SwapCounterOffers",
                column: "OfferedShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapCounterOffers_ProposedByMedicoId",
                table: "SwapCounterOffers",
                column: "ProposedByMedicoId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapCounterOffers_SwapRequestId",
                table: "SwapCounterOffers",
                column: "SwapRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SwapCounterOffers");
        }
    }
}
