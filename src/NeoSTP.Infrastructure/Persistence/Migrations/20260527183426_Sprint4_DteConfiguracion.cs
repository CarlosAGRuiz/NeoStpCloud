using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_DteConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_Configuracion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    AmbienteCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UsuarioMh = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PasswordMhCifrado = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TipoEstablecimientoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CodigoEstablecimientoMh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodigoPuntoVentaMh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CertificadoBlob = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CertificadoNombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CertificadoHuella = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertificadoEmitido = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertificadoVence = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordCertificadoCifrado = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UltimaPruebaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaPruebaResultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UltimaPruebaDetalle = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TokenMhCifrado = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TokenMhExpiraAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_Configuracion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_Configuracion_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 19,
                column: "Descripcion",
                value: "42 municipios post-reforma territorial 2024 (Decreto 290). Cada item lleva departamento padre + zona en metadata. Distribución base; ajustar contra CAT-013 final de Hacienda");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Configuracion_EmpresaId",
                table: "Dte_Configuracion",
                column: "EmpresaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_Configuracion");

            migrationBuilder.UpdateData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 19,
                column: "Descripcion",
                value: "44 municipios post-reforma 2024 (Decreto 290). Cada item incluye departamento padre en metadata");
        }
    }
}
