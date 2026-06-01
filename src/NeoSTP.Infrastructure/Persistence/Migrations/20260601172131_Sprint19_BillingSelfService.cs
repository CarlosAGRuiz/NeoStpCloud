using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint19_BillingSelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Billing_Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ExternalCustomerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billing_Customers_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Billing_PlanProviderMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExternalPlanId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalProductId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_PlanProviderMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billing_PlanProviderMappings_Core_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Core_Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Billing_WebhookEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Processed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_WebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Billing_Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingCustomerId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    ExternalSubscriptionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TrialStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrialEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billing_Subscriptions_Billing_Customers_BillingCustomerId",
                        column: x => x.BillingCustomerId,
                        principalTable: "Billing_Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Billing_Subscriptions_Core_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Core_Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Billing_Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    ExternalInvoiceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PdfUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billing_Invoices_Billing_Subscriptions_BillingSubscriptionId",
                        column: x => x.BillingSubscriptionId,
                        principalTable: "Billing_Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Billing_Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    ExternalPaymentId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceiptUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billing_Payments_Billing_Subscriptions_BillingSubscriptionId",
                        column: x => x.BillingSubscriptionId,
                        principalTable: "Billing_Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_Customers_EmpresaId",
                table: "Billing_Customers",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_Customers_Provider_ExternalCustomerId",
                table: "Billing_Customers",
                columns: new[] { "Provider", "ExternalCustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_Invoices_BillingSubscriptionId",
                table: "Billing_Invoices",
                column: "BillingSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_Payments_BillingSubscriptionId",
                table: "Billing_Payments",
                column: "BillingSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_PlanProviderMappings_PlanId_Provider",
                table: "Billing_PlanProviderMappings",
                columns: new[] { "PlanId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_Subscriptions_BillingCustomerId_Status",
                table: "Billing_Subscriptions",
                columns: new[] { "BillingCustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_Subscriptions_ExternalSubscriptionId",
                table: "Billing_Subscriptions",
                column: "ExternalSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_Subscriptions_PlanId",
                table: "Billing_Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_WebhookEvents_Processed",
                table: "Billing_WebhookEvents",
                column: "Processed");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_WebhookEvents_Provider_ExternalEventId",
                table: "Billing_WebhookEvents",
                columns: new[] { "Provider", "ExternalEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Billing_Invoices");

            migrationBuilder.DropTable(
                name: "Billing_Payments");

            migrationBuilder.DropTable(
                name: "Billing_PlanProviderMappings");

            migrationBuilder.DropTable(
                name: "Billing_WebhookEvents");

            migrationBuilder.DropTable(
                name: "Billing_Subscriptions");

            migrationBuilder.DropTable(
                name: "Billing_Customers");
        }
    }
}
