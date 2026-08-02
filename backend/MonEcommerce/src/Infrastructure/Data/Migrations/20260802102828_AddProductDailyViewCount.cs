using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonEcommerce.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductDailyViewCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductDailyViewCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDailyViewCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductDailyViewCounts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_daily_view_counts_product_id_date",
                table: "ProductDailyViewCounts",
                columns: new[] { "ProductId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductDailyViewCounts");
        }
    }
}
