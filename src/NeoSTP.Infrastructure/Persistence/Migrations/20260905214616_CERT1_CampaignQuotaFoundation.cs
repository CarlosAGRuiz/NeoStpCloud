using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CERT1_CampaignQuotaFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_CertificationCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ExpectedNit = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: false),
                    AmbienteCodigo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TotalBudget = table.Column<int>(type: "int", nullable: false),
                    MatrixReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_CertificationCampaigns", x => x.Id);
                    table.CheckConstraint("CK_CertificationCampaign_Budget", "[TotalBudget] > 0");
                    table.CheckConstraint("CK_CertificationCampaign_Nit", "LEN([ExpectedNit]) = 14 AND [ExpectedNit] NOT LIKE '%[^0-9]%'");
                    table.CheckConstraint("CK_CertificationCampaign_Status", "[Status] IN ('PREPARED','ACTIVE','REVOKED','CLOSED')");
                    table.CheckConstraint("CK_CertificationCampaign_TestOnly", "[AmbienteCodigo] = 'PRUEBAS'");
                    table.CheckConstraint("CK_CertificationCampaign_UtcPeriod", "[ExpiresAtUtc] > [StartsAtUtc] AND DATEPART(TZOFFSET,[StartsAtUtc]) = 0 AND DATEPART(TZOFFSET,[ExpiresAtUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_Dte_CertificationCampaigns_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dte_CertificationCampaignTypeBudgets",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    TipoDteCodigo = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    Budget = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_CertificationCampaignTypeBudgets", x => new { x.CampaignId, x.TipoDteCodigo });
                    table.CheckConstraint("CK_CertificationCampaignType_Budget", "[Budget] > 0");
                    table.CheckConstraint("CK_CertificationCampaignType_Type", "[TipoDteCodigo] IN ('01','03','11','14')");
                    table.ForeignKey(
                        name: "FK_Dte_CertificationCampaignTypeBudgets_Dte_CertificationCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Dte_CertificationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dte_CertificationCampaignConsumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    TipoDteCodigo = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    DteDocumentoId = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ScenarioReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_CertificationCampaignConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_CertificationCampaignConsumptions_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dte_CertificationCampaignConsumptions_Dte_CertificationCampaignTypeBudgets_CampaignId_TipoDteCodigo",
                        columns: x => new { x.CampaignId, x.TipoDteCodigo },
                        principalTable: "Dte_CertificationCampaignTypeBudgets",
                        principalColumns: new[] { "CampaignId", "TipoDteCodigo" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dte_CertificationCampaignConsumptions_Dte_Documentos_DteDocumentoId",
                        column: x => x.DteDocumentoId,
                        principalTable: "Dte_Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_CertificationCampaignConsumptions_CampaignId_IdempotencyKeyHash",
                table: "Dte_CertificationCampaignConsumptions",
                columns: new[] { "CampaignId", "IdempotencyKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_CertificationCampaignConsumptions_CampaignId_TipoDteCodigo",
                table: "Dte_CertificationCampaignConsumptions",
                columns: new[] { "CampaignId", "TipoDteCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_CertificationCampaignConsumptions_DteDocumentoId",
                table: "Dte_CertificationCampaignConsumptions",
                column: "DteDocumentoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_CertificationCampaignConsumptions_EmpresaId",
                table: "Dte_CertificationCampaignConsumptions",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_CertificationCampaignConsumptions_PublicId",
                table: "Dte_CertificationCampaignConsumptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_CertificationCampaigns_EmpresaId_Status",
                table: "Dte_CertificationCampaigns",
                columns: new[] { "EmpresaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_CertificationCampaigns_PublicId",
                table: "Dte_CertificationCampaigns",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_CertificationCampaignConsumptions");

            migrationBuilder.DropTable(
                name: "Dte_CertificationCampaignTypeBudgets");

            migrationBuilder.DropTable(
                name: "Dte_CertificationCampaigns");
        }
    }
}
