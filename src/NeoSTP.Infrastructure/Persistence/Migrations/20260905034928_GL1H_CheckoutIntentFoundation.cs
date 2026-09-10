using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GL1H_CheckoutIntentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Billing_CheckoutIntents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    BillingCustomerId = table.Column<int>(type: "int", nullable: true),
                    BillingSubscriptionId = table.Column<int>(type: "int", nullable: true),
                    EmpresaPlanId = table.Column<int>(type: "int", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BillingInterval = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExternalPlanId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalCustomerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SuccessUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CancelUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LeaseId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExternalCheckoutId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RedirectUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProviderAcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_CheckoutIntents", x => x.Id);
                    table.CheckConstraint("CK_Billing_CheckoutIntents_Ack", "[Status] <> 'AWAITING_PAYMENT' OR ([ProviderAcknowledgedAt] IS NOT NULL AND [ExternalCheckoutId] IS NOT NULL AND [RedirectUrl] IS NOT NULL)");
                    table.CheckConstraint("CK_Billing_CheckoutIntents_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Billing_CheckoutIntents_Billing_Customers_BillingCustomerId",
                        column: x => x.BillingCustomerId,
                        principalTable: "Billing_Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CheckoutIntents_Billing_Subscriptions_BillingSubscriptionId",
                        column: x => x.BillingSubscriptionId,
                        principalTable: "Billing_Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CheckoutIntents_Core_EmpresaPlan_EmpresaPlanId",
                        column: x => x.EmpresaPlanId,
                        principalTable: "Core_EmpresaPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CheckoutIntents_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CheckoutIntents_Core_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Core_Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_BillingCustomerId",
                table: "Billing_CheckoutIntents",
                column: "BillingCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_BillingSubscriptionId",
                table: "Billing_CheckoutIntents",
                column: "BillingSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_CorrelationId",
                table: "Billing_CheckoutIntents",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_EmpresaId_IdempotencyKeyHash",
                table: "Billing_CheckoutIntents",
                columns: new[] { "EmpresaId", "IdempotencyKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_EmpresaId_Status",
                table: "Billing_CheckoutIntents",
                columns: new[] { "EmpresaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_EmpresaPlanId",
                table: "Billing_CheckoutIntents",
                column: "EmpresaPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_PlanId",
                table: "Billing_CheckoutIntents",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_Provider_ProviderAccountId_ExternalCheckoutId",
                table: "Billing_CheckoutIntents",
                columns: new[] { "Provider", "ProviderAccountId", "ExternalCheckoutId" },
                unique: true,
                filter: "[ExternalCheckoutId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CheckoutIntents_Status_LeaseExpiresAt",
                table: "Billing_CheckoutIntents",
                columns: new[] { "Status", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Billing_CheckoutIntents");
        }
    }
}
