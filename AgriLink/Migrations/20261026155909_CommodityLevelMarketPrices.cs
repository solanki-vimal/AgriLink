using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriLink.Migrations
{
    /// <inheritdoc />
    public partial class CommodityLevelMarketPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "MarketPrices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "MarketCount",
                table: "MarketPrices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxPrice",
                table: "MarketPrices",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinPrice",
                table: "MarketPrices",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "MarketPrices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketCount",
                table: "MarketPrices");

            migrationBuilder.DropColumn(
                name: "MaxPrice",
                table: "MarketPrices");

            migrationBuilder.DropColumn(
                name: "MinPrice",
                table: "MarketPrices");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "MarketPrices");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "MarketPrices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
