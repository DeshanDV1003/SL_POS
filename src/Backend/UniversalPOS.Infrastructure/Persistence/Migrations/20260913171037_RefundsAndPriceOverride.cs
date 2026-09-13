using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefundsAndPriceOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OriginalSaleHeaderId",
                table: "SaleHeaders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "SaleHeaders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalSaleHeaderId",
                table: "SaleHeaders");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "SaleHeaders");
        }
    }
}
