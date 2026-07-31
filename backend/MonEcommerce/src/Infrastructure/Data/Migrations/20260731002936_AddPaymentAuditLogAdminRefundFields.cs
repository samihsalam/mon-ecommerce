using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonEcommerce.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAuditLogAdminRefundFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminUserId",
                table: "PaymentAuditLogs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeRefundId",
                table: "PaymentAuditLogs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminUserId",
                table: "PaymentAuditLogs");

            migrationBuilder.DropColumn(
                name: "StripeRefundId",
                table: "PaymentAuditLogs");
        }
    }
}
