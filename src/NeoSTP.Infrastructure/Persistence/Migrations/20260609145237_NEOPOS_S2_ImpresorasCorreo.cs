using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NEOPOS_S2_ImpresorasCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Com_ConfiguracionCorreo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Puerto = table.Column<int>(type: "int", nullable: false),
                    UsarStartTls = table.Column<bool>(type: "bit", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    PasswordProtegida = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    FromNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FromEmail = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Com_ConfiguracionCorreo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Com_ConfiguracionCorreo_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pos_Impresoras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    PuntoVentaId = table.Column<int>(type: "int", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Conexion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnchoMm = table.Column<int>(type: "int", nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Puerto = table.Column<int>(type: "int", nullable: false),
                    CorteAutomatico = table.Column<bool>(type: "bit", nullable: false),
                    EsPredeterminada = table.Column<bool>(type: "bit", nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pos_Impresoras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pos_Impresoras_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 316, "Core.Correo.Configurar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Configurar el correo saliente (SMTP) de la empresa", "CORE", null, null });

            migrationBuilder.InsertData(
                table: "Core_RolPermisos",
                columns: new[] { "PermisoId", "RolId", "CreatedAt" },
                values: new object[,]
                {
                    { 316, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 316, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Com_ConfiguracionCorreo_EmpresaId",
                table: "Com_ConfiguracionCorreo",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pos_Impresoras_EmpresaId_EstadoCodigo",
                table: "Pos_Impresoras",
                columns: new[] { "EmpresaId", "EstadoCodigo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Com_ConfiguracionCorreo");

            migrationBuilder.DropTable(
                name: "Pos_Impresoras");

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 316, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 316, 501 });

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 316);
        }
    }
}
