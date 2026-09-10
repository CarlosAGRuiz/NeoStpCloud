using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GL1H_WompiPaymentInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProduction",
                table: "Billing_CheckoutIntents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Billing_PaymentNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsProduction = table.Column<bool>(type: "bit", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckoutCorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingCheckoutIntentId = table.Column<int>(type: "int", nullable: true),
                    ExternalCheckoutId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TransactionAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SemanticHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LeaseId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderPaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_PaymentNotifications", x => x.Id);
                    table.CheckConstraint("CK_Billing_PaymentNotifications_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Billing_PaymentNotifications_Verified", "[Status] <> 'VERIFIED_SANDBOX' OR ([IsProduction] = 0 AND [VerifiedAt] IS NOT NULL AND [ProviderPaidAt] IS NOT NULL AND [BillingCheckoutIntentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Billing_PaymentNotifications_Billing_CheckoutIntents_BillingCheckoutIntentId",
                        column: x => x.BillingCheckoutIntentId,
                        principalTable: "Billing_CheckoutIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentNotifications_BillingCheckoutIntentId",
                table: "Billing_PaymentNotifications",
                column: "BillingCheckoutIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentNotifications_CheckoutCorrelationId_Status",
                table: "Billing_PaymentNotifications",
                columns: new[] { "CheckoutCorrelationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentNotifications_Provider_ProviderAccountId_IsProduction_TransactionId",
                table: "Billing_PaymentNotifications",
                columns: new[] { "Provider", "ProviderAccountId", "IsProduction", "TransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentNotifications_ReceiptId",
                table: "Billing_PaymentNotifications",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentNotifications_Status_LeaseExpiresAt",
                table: "Billing_PaymentNotifications",
                columns: new[] { "Status", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Billing_PaymentNotifications");

            migrationBuilder.DropColumn(
                name: "IsProduction",
                table: "Billing_CheckoutIntents");
        }
    }
}
