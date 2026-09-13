using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P4_DteConfiguracionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_ConfiguracionVersiones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ConfiguracionId = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AmbienteCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TiposDteAutorizadosCsv = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_ConfiguracionVersiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_ConfiguracionVersiones_Dte_Configuracion_ConfiguracionId",
                        column: x => x.ConfiguracionId,
                        principalTable: "Dte_Configuracion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ConfiguracionVersiones_ConfiguracionId",
                table: "Dte_ConfiguracionVersiones",
                column: "ConfiguracionId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ConfiguracionVersiones_EmpresaId_CreatedAt",
                table: "Dte_ConfiguracionVersiones",
                columns: new[] { "EmpresaId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_ConfiguracionVersiones_EmpresaId_Id",
                table: "Dte_ConfiguracionVersiones",
                columns: new[] { "EmpresaId", "Id" });

            // Baseline para que las empresas existentes dispongan inmediatamente
            // de un punto de recuperación. Los secretos se copian ya cifrados.
            migrationBuilder.Sql("""
                INSERT INTO Dte_ConfiguracionVersiones
                    (EmpresaId, ConfiguracionId, Motivo, AmbienteCodigo, TiposDteAutorizadosCsv,
                     UsuarioMh, PasswordMhCifrado, TipoEstablecimientoCodigo,
                     CodigoEstablecimientoMh, CodigoPuntoVentaMh, CertificadoBlob,
                     CertificadoNombre, CertificadoHuella, CertificadoEmitido, CertificadoVence,
                     PasswordCertificadoCifrado, CreatedAt, CreatedBy)
                SELECT EmpresaId, Id, N'BASELINE', AmbienteCodigo, TiposDteAutorizadosCsv,
                       UsuarioMh, PasswordMhCifrado, TipoEstablecimientoCodigo,
                       CodigoEstablecimientoMh, CodigoPuntoVentaMh, CertificadoBlob,
                       CertificadoNombre, CertificadoHuella, CertificadoEmitido, CertificadoVence,
                       PasswordCertificadoCifrado, SYSUTCDATETIME(), N'MIGRATION_P4'
                FROM Dte_Configuracion;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_ConfiguracionVersiones");
        }
    }
}
