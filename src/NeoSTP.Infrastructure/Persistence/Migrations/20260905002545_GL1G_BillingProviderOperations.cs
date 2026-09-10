using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GL1G_BillingProviderOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Billing_ProviderOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    BillingSubscriptionId = table.Column<int>(type: "int", nullable: true),
                    EmpresaPlanId = table.Column<int>(type: "int", nullable: true),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalResourceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccessEndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billing_ProviderOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billing_ProviderOperations_Billing_Subscriptions_BillingSubscriptionId",
                        column: x => x.BillingSubscriptionId,
                        principalTable: "Billing_Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_ProviderOperations_Core_EmpresaPlan_EmpresaPlanId",
                        column: x => x.EmpresaPlanId,
                        principalTable: "Core_EmpresaPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_ProviderOperations_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Billing_ProviderOperations_Core_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Core_Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_ProviderOperations_BillingSubscriptionId",
                table: "Billing_ProviderOperations",
                column: "BillingSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_ProviderOperations_EmpresaId",
                table: "Billing_ProviderOperations",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_ProviderOperations_EmpresaId_PlanId",
                table: "Billing_ProviderOperations",
                columns: new[] { "EmpresaId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_ProviderOperations_EmpresaPlanId",
                table: "Billing_ProviderOperations",
                column: "EmpresaPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_ProviderOperations_IdempotencyKey",
                table: "Billing_ProviderOperations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_ProviderOperations_PlanId",
                table: "Billing_ProviderOperations",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_ProviderOperations_Status_NextAttemptAt_LeaseExpiresAt",
                table: "Billing_ProviderOperations",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Billing_ProviderOperations");
        }
    }
}
