using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GL1H_AtomicPaymentApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommercialSnapshotJson",
                table: "Billing_CheckoutIntents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Billing_PaymentApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingCheckoutIntentId = table.Column<int>(type: "int", nullable: false),
                    BillingPaymentNotificationId = table.Column<int>(type: "int", nullable: false),
                    BillingPaymentId = table.Column<int>(type: "int", nullable: false),
                    BillingSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    EmpresaPlanId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModuleIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommercialSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_PaymentApplications", x => x.Id);
                    table.CheckConstraint("CK_Billing_PaymentApplications_Period", "[PeriodEnd] > [PeriodStart]");
                    table.ForeignKey(
                        name: "FK_Billing_PaymentApplications_Billing_CheckoutIntents_BillingCheckoutIntentId",
                        column: x => x.BillingCheckoutIntentId,
                        principalTable: "Billing_CheckoutIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_PaymentApplications_Billing_PaymentNotifications_BillingPaymentNotificationId",
                        column: x => x.BillingPaymentNotificationId,
                        principalTable: "Billing_PaymentNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_PaymentApplications_Billing_Payments_BillingPaymentId",
                        column: x => x.BillingPaymentId,
                        principalTable: "Billing_Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_PaymentApplications_Billing_Subscriptions_BillingSubscriptionId",
                        column: x => x.BillingSubscriptionId,
                        principalTable: "Billing_Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_PaymentApplications_Core_EmpresaPlan_EmpresaPlanId",
                        column: x => x.EmpresaPlanId,
                        principalTable: "Core_EmpresaPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Billing_PaymentNotifications_Capture",
                table: "Billing_PaymentNotifications",
                sql: "[Status] <> 'VERIFIED_CAPTURED_PRODUCTION' OR ([IsProduction] = 1 AND [VerifiedAt] IS NOT NULL AND [ProviderPaidAt] IS NOT NULL AND [BillingCheckoutIntentId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentApplications_BillingCheckoutIntentId",
                table: "Billing_PaymentApplications",
                column: "BillingCheckoutIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentApplications_BillingPaymentId",
                table: "Billing_PaymentApplications",
                column: "BillingPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentApplications_BillingPaymentNotificationId",
                table: "Billing_PaymentApplications",
                column: "BillingPaymentNotificationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentApplications_BillingSubscriptionId",
                table: "Billing_PaymentApplications",
                column: "BillingSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PaymentApplications_EmpresaPlanId",
                table: "Billing_PaymentApplications",
                column: "EmpresaPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Billing_PaymentApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Billing_PaymentNotifications_Capture",
                table: "Billing_PaymentNotifications");

            migrationBuilder.DropColumn(
                name: "CommercialSnapshotJson",
                table: "Billing_CheckoutIntents");
        }
    }
}
