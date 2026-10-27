using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriLink.Migrations
{
    /// <inheritdoc />
    public partial class AddCommodityNameToMarketPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommodityName",
                table: "MarketPrices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommodityName",
                table: "MarketPrices");
        }
    }
}
