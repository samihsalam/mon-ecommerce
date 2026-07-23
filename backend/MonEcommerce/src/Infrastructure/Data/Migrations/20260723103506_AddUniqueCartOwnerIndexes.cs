using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonEcommerce.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCartOwnerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_carts_session_id",
                table: "Carts");

            migrationBuilder.DropIndex(
                name: "ix_carts_user_id",
                table: "Carts");

            migrationBuilder.CreateIndex(
                name: "ix_carts_session_id",
                table: "Carts",
                column: "SessionId",
                unique: true,
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_carts_user_id",
                table: "Carts",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_carts_session_id",
                table: "Carts");

            migrationBuilder.DropIndex(
                name: "ix_carts_user_id",
                table: "Carts");

            migrationBuilder.CreateIndex(
                name: "ix_carts_session_id",
                table: "Carts",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "ix_carts_user_id",
                table: "Carts",
                column: "UserId");
        }
    }
}
