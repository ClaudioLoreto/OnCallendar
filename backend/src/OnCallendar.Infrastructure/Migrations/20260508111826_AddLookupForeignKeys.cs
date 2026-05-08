using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnCallendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLookupForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FK constraint a livello DB sulle colonne enum-as-string verso le
            // tabelle di lookup. EF non li genera in automatico perche` la
            // colonna ha un value converter (enum -> string), ma i tipi
            // sottostanti sono compatibili.
            migrationBuilder.Sql(@"
ALTER TABLE ""AspNetUsers""
ADD CONSTRAINT ""FK_AspNetUsers_RoleTypes_Role""
FOREIGN KEY (""Role"") REFERENCES ""RoleTypes""(""Code"")
ON UPDATE CASCADE ON DELETE RESTRICT;");

            migrationBuilder.Sql(@"
ALTER TABLE ""Shifts""
ADD CONSTRAINT ""FK_Shifts_ShiftTypes_Code""
FOREIGN KEY (""Code"") REFERENCES ""ShiftTypes""(""Code"")
ON UPDATE CASCADE ON DELETE RESTRICT;");

            migrationBuilder.Sql(@"
ALTER TABLE ""Shifts""
ADD CONSTRAINT ""FK_Shifts_ShiftStatuses_Status""
FOREIGN KEY (""Status"") REFERENCES ""ShiftStatuses""(""Code"")
ON UPDATE CASCADE ON DELETE RESTRICT;");

            migrationBuilder.Sql(@"
ALTER TABLE ""SwapRequests""
ADD CONSTRAINT ""FK_SwapRequests_SwapRequestTypes_Type""
FOREIGN KEY (""Type"") REFERENCES ""SwapRequestTypes""(""Code"")
ON UPDATE CASCADE ON DELETE RESTRICT;");

            migrationBuilder.Sql(@"
ALTER TABLE ""SwapRequests""
ADD CONSTRAINT ""FK_SwapRequests_SwapRequestStatuses_Status""
FOREIGN KEY (""Status"") REFERENCES ""SwapRequestStatuses""(""Code"")
ON UPDATE CASCADE ON DELETE RESTRICT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""SwapRequests"" DROP CONSTRAINT IF EXISTS ""FK_SwapRequests_SwapRequestStatuses_Status"";");
            migrationBuilder.Sql(@"ALTER TABLE ""SwapRequests"" DROP CONSTRAINT IF EXISTS ""FK_SwapRequests_SwapRequestTypes_Type"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Shifts"" DROP CONSTRAINT IF EXISTS ""FK_Shifts_ShiftStatuses_Status"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Shifts"" DROP CONSTRAINT IF EXISTS ""FK_Shifts_ShiftTypes_Code"";");
            migrationBuilder.Sql(@"ALTER TABLE ""AspNetUsers"" DROP CONSTRAINT IF EXISTS ""FK_AspNetUsers_RoleTypes_Role"";");
        }
    }
}
