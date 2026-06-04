using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class B3_NeoScanAI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_DocumentosRecibidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    EmisorNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    EmisorNit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EmisorNrc = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoDteCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    NumeroControl = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SelloRecibido = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Iva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScanDocumentoId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_DocumentosRecibidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_DocumentosRecibidos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Scan_Documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TipoClasificacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ArchivoBlob = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ArchivoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ArchivoNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmisorNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    EmisorNit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EmisorNrc = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    TipoDocumento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    NumeroControl = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SelloRecibido = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Iva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Confianza = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProfitGastoId = table.Column<int>(type: "int", nullable: true),
                    ProfitCompraId = table.Column<int>(type: "int", nullable: true),
                    DteRecibidoId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scan_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scan_Documentos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_DocumentosRecibidos_EmpresaId_Fecha",
                table: "Dte_DocumentosRecibidos",
                columns: new[] { "EmpresaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Scan_Documentos_EmpresaId_EstadoCodigo",
                table: "Scan_Documentos",
                columns: new[] { "EmpresaId", "EstadoCodigo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_DocumentosRecibidos");

            migrationBuilder.DropTable(
                name: "Scan_Documentos");
        }
    }
}
