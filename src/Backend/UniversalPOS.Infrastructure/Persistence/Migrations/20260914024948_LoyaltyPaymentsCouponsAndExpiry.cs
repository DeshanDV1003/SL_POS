using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyPaymentsCouponsAndExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CouponDiscountAmount",
                table: "SaleHeaders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "CouponId",
                table: "SaleHeaders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "LoyaltyTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExpired",
                table: "LoyaltyTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyPointRedemptionValue",
                table: "Companies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyPointsExpiryMonths",
                table: "Companies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyPointsPerCurrencyUnit",
                table: "Companies",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinSaleAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxRedemptions = table.Column<int>(type: "int", nullable: true),
                    TimesRedeemed = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_CompanyId_Code",
                table: "Coupons",
                columns: new[] { "CompanyId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropColumn(
                name: "CouponDiscountAmount",
                table: "SaleHeaders");

            migrationBuilder.DropColumn(
                name: "CouponId",
                table: "SaleHeaders");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "LoyaltyTransactions");

            migrationBuilder.DropColumn(
                name: "IsExpired",
                table: "LoyaltyTransactions");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointRedemptionValue",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointsExpiryMonths",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointsPerCurrencyUnit",
                table: "Companies");
        }
    }
}
