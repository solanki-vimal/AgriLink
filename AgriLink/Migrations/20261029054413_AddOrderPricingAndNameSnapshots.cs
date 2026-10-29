using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriLink.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPricingAndNameSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FarmerName",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProduceName",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "Orders",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill existing rows with UnitPrice = TotalPrice / QuantityOrdered
            migrationBuilder.Sql(
                @"UPDATE Orders
                  SET UnitPrice = CASE WHEN QuantityOrdered > 0 THEN ROUND(TotalPrice / QuantityOrdered, 2) ELSE 0 END;");

            // Backfill snapshots from Produce and Farmer user records
            migrationBuilder.Sql(
                @"UPDATE o
                  SET o.ProduceName = ISNULL(p.Name, ''),
                      o.Unit = ISNULL(p.Unit, ''),
                      o.FarmerName = ISNULL(u.FullName, '')
                  FROM Orders o
                  INNER JOIN ProduceListings p ON o.ProduceId = p.ProduceId
                  LEFT JOIN AspNetUsers u ON p.FarmerId = u.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProduceName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "Orders");
        }
    }
}
