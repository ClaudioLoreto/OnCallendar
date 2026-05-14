using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnCallendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSwapRequestIsReperibile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwapCounterOffers_AspNetUsers_ProposedByMedicoId",
                table: "SwapCounterOffers");

            migrationBuilder.DropForeignKey(
                name: "FK_SwapCounterOffers_Shifts_OfferedShiftId",
                table: "SwapCounterOffers");

            migrationBuilder.DropIndex(
                name: "IX_SwapRequests_InitiatorShiftId",
                table: "SwapRequests");

            migrationBuilder.AddColumn<bool>(
                name: "IsReperibile",
                table: "SwapRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SwapCounterOffers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "SwapCounterOffers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwapRequests_InitiatorShiftId_Status",
                table: "SwapRequests",
                columns: new[] { "InitiatorShiftId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_SwapCounterOffers_AspNetUsers_ProposedByMedicoId",
                table: "SwapCounterOffers",
                column: "ProposedByMedicoId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SwapCounterOffers_Shifts_OfferedShiftId",
                table: "SwapCounterOffers",
                column: "OfferedShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwapCounterOffers_AspNetUsers_ProposedByMedicoId",
                table: "SwapCounterOffers");

            migrationBuilder.DropForeignKey(
                name: "FK_SwapCounterOffers_Shifts_OfferedShiftId",
                table: "SwapCounterOffers");

            migrationBuilder.DropIndex(
                name: "IX_SwapRequests_InitiatorShiftId_Status",
                table: "SwapRequests");

            migrationBuilder.DropColumn(
                name: "IsReperibile",
                table: "SwapRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SwapCounterOffers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "SwapCounterOffers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwapRequests_InitiatorShiftId",
                table: "SwapRequests",
                column: "InitiatorShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_SwapCounterOffers_AspNetUsers_ProposedByMedicoId",
                table: "SwapCounterOffers",
                column: "ProposedByMedicoId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SwapCounterOffers_Shifts_OfferedShiftId",
                table: "SwapCounterOffers",
                column: "OfferedShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
