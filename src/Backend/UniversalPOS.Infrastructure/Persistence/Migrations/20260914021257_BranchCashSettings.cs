using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BranchCashSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BusinessDayCutoffHour",
                table: "Branches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireOpenShiftForSale",
                table: "Branches",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessDayCutoffHour",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "RequireOpenShiftForSale",
                table: "Branches");
        }
    }
}
