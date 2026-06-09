using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NeoStpDbContext))]
    [Migration("20260609203000_V2_D3_RecordatoriosCobro")]
    public partial class V2_D3_RecordatoriosCobro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cobros_Recordatorios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    DteDocumentoId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    FechaRecordatorio = table.Column<DateOnly>(type: "date", nullable: false),
                    Canal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Destinatario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MessageId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Saldo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiasVencido = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cobros_Recordatorios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cobros_Recordatorios_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobros_Recordatorios_Dte_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Dte_Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobros_Recordatorios_Dte_Documentos_DteDocumentoId",
                        column: x => x.DteDocumentoId,
                        principalTable: "Dte_Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_Recordatorios_ClienteId",
                table: "Cobros_Recordatorios",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_Recordatorios_DteDocumentoId",
                table: "Cobros_Recordatorios",
                column: "DteDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_Recordatorios_EmpresaId_DteDocumentoId_Canal_FechaRecordatorio",
                table: "Cobros_Recordatorios",
                columns: new[] { "EmpresaId", "DteDocumentoId", "Canal", "FechaRecordatorio" });

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_Recordatorios_EmpresaId_FechaRecordatorio_EstadoCodigo",
                table: "Cobros_Recordatorios",
                columns: new[] { "EmpresaId", "FechaRecordatorio", "EstadoCodigo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cobros_Recordatorios");
        }
    }
}
