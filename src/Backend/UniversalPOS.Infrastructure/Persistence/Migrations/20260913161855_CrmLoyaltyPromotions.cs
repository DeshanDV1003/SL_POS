using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CrmLoyaltyPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MembershipTierId",
                table: "Customers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    PointsChange = table.Column<int>(type: "int", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipTiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinimumPoints = table.Column<int>(type: "int", nullable: false),
                    PointsMultiplier = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipTiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    MinQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_MembershipTierId",
                table: "Customers",
                column: "MembershipTierId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerId",
                table: "LoyaltyTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTiers_CompanyId_Name",
                table: "MembershipTiers",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CompanyId_IsActive",
                table: "Promotions",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_MembershipTiers_MembershipTierId",
                table: "Customers",
                column: "MembershipTierId",
                principalTable: "MembershipTiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_MembershipTiers_MembershipTierId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropTable(
                name: "MembershipTiers");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Customers_MembershipTierId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MembershipTierId",
                table: "Customers");
        }
    }
}
