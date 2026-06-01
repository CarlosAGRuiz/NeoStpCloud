using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint17_DiagnosticoErrores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_ErrorCatalogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MensajeTecnico = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CausaProbable = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AccionSugerida = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Severidad = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_ErrorCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dte_ErrorOcurrencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    CodigoError = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DteDocumentoId = table.Column<int>(type: "int", nullable: true),
                    DteEventoId = table.Column<int>(type: "int", nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RespuestaMhJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JsonEnviado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fuente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OcurrioAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resuelta = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_ErrorOcurrencias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ErrorCatalogo_Codigo",
                table: "Dte_ErrorCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ErrorCatalogo_Tipo",
                table: "Dte_ErrorCatalogo",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ErrorOcurrencias_DteDocumentoId",
                table: "Dte_ErrorOcurrencias",
                column: "DteDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ErrorOcurrencias_DteEventoId",
                table: "Dte_ErrorOcurrencias",
                column: "DteEventoId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ErrorOcurrencias_EmpresaId_CodigoError",
                table: "Dte_ErrorOcurrencias",
                columns: new[] { "EmpresaId", "CodigoError" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ErrorOcurrencias_EmpresaId_OcurrioAt",
                table: "Dte_ErrorOcurrencias",
                columns: new[] { "EmpresaId", "OcurrioAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_ErrorCatalogo");

            migrationBuilder.DropTable(
                name: "Dte_ErrorOcurrencias");
        }
    }
}
