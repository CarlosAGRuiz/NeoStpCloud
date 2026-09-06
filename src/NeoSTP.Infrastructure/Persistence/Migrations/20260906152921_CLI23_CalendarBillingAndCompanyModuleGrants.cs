using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CLI23_CalendarBillingAndCompanyModuleGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ComplementoAutorizado",
                table: "Core_EmpresaModulos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ComplementoAutorizadoAt",
                table: "Core_EmpresaModulos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplementoAutorizadoBy",
                table: "Core_EmpresaModulos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplementoMotivo",
                table: "Core_EmpresaModulos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Billing_CalendarAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    BillingSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    EmpresaPlanId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FirstPeriodStartLocal = table.Column<DateOnly>(type: "date", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    AutomaticSuspension = table.Column<bool>(type: "bit", nullable: false),
                    AgreementKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_CalendarAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billing_CalendarAgreements_Billing_Subscriptions_BillingSubscriptionId",
                        column: x => x.BillingSubscriptionId,
                        principalTable: "Billing_Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CalendarAgreements_Core_EmpresaPlan_EmpresaPlanId",
                        column: x => x.EmpresaPlanId,
                        principalTable: "Core_EmpresaPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CalendarAgreements_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CalendarAgreements_Core_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Core_Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Billing_CalendarPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgreementId = table.Column<int>(type: "int", nullable: false),
                    PeriodStartLocal = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEndExclusiveLocal = table.Column<DateOnly>(type: "date", nullable: false),
                    DueLocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BillingInvoiceId = table.Column<int>(type: "int", nullable: false),
                    BillingPaymentId = table.Column<int>(type: "int", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_CalendarPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billing_CalendarPeriods_Billing_CalendarAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "Billing_CalendarAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CalendarPeriods_Billing_Invoices_BillingInvoiceId",
                        column: x => x.BillingInvoiceId,
                        principalTable: "Billing_Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_CalendarPeriods_Billing_Payments_BillingPaymentId",
                        column: x => x.BillingPaymentId,
                        principalTable: "Billing_Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarAgreements_AgreementKey",
                table: "Billing_CalendarAgreements",
                column: "AgreementKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarAgreements_BillingSubscriptionId",
                table: "Billing_CalendarAgreements",
                column: "BillingSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarAgreements_EmpresaId",
                table: "Billing_CalendarAgreements",
                column: "EmpresaId",
                unique: true,
                filter: "[Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarAgreements_EmpresaPlanId",
                table: "Billing_CalendarAgreements",
                column: "EmpresaPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarAgreements_PlanId",
                table: "Billing_CalendarAgreements",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarPeriods_AgreementId_PaymentReference",
                table: "Billing_CalendarPeriods",
                columns: new[] { "AgreementId", "PaymentReference" },
                unique: true,
                filter: "[PaymentReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarPeriods_AgreementId_PeriodStartLocal",
                table: "Billing_CalendarPeriods",
                columns: new[] { "AgreementId", "PeriodStartLocal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarPeriods_BillingInvoiceId",
                table: "Billing_CalendarPeriods",
                column: "BillingInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_CalendarPeriods_BillingPaymentId",
                table: "Billing_CalendarPeriods",
                column: "BillingPaymentId",
                unique: true,
                filter: "[BillingPaymentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Billing_CalendarPeriods");

            migrationBuilder.DropTable(
                name: "Billing_CalendarAgreements");

            migrationBuilder.DropColumn(
                name: "ComplementoAutorizado",
                table: "Core_EmpresaModulos");

            migrationBuilder.DropColumn(
                name: "ComplementoAutorizadoAt",
                table: "Core_EmpresaModulos");

            migrationBuilder.DropColumn(
                name: "ComplementoAutorizadoBy",
                table: "Core_EmpresaModulos");

            migrationBuilder.DropColumn(
                name: "ComplementoMotivo",
                table: "Core_EmpresaModulos");
        }
    }
}
