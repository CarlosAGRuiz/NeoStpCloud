using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint16_ContingenciaLotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_ContingenciaLotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    EventoContingenciaId = table.Column<int>(type: "int", nullable: false),
                    CodigoLote = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SelloRecibido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AmbienteCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EnviadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaConsultaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawEnvio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawConsulta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_ContingenciaLotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_ContingenciaLotes_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dte_ContingenciaLoteDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    DteDocumentoId = table.Column<int>(type: "int", nullable: false),
                    CodigoGeneracion = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TipoDteCodigo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    SelloRecibido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MensajeHacienda = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_ContingenciaLoteDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_ContingenciaLoteDetalles_Dte_ContingenciaLotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Dte_ContingenciaLotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ContingenciaLoteDetalles_DteDocumentoId",
                table: "Dte_ContingenciaLoteDetalles",
                column: "DteDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ContingenciaLoteDetalles_LoteId_CodigoGeneracion",
                table: "Dte_ContingenciaLoteDetalles",
                columns: new[] { "LoteId", "CodigoGeneracion" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ContingenciaLotes_EmpresaId_EstadoCodigo",
                table: "Dte_ContingenciaLotes",
                columns: new[] { "EmpresaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ContingenciaLotes_EventoContingenciaId",
                table: "Dte_ContingenciaLotes",
                column: "EventoContingenciaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_ContingenciaLoteDetalles");

            migrationBuilder.DropTable(
                name: "Dte_ContingenciaLotes");
        }
    }
}
