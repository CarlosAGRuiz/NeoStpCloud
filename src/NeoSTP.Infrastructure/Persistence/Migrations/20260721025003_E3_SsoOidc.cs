using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class E3_SsoOidc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SsoProveedor",
                table: "Core_Usuarios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SsoSubject",
                table: "Core_Usuarios",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Core_EmpresaSso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ProveedorCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Habilitado = table.Column<bool>(type: "bit", nullable: false),
                    DominioCorreo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantIdExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AutoProvisionar = table.Column<bool>(type: "bit", nullable: false),
                    RolPorDefectoId = table.Column<int>(type: "int", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_EmpresaSso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_EmpresaSso_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Core_EmpresaSso_Core_Roles_RolPorDefectoId",
                        column: x => x.RolPorDefectoId,
                        principalTable: "Core_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 425, "Seguridad.Sso.Gestionar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Configurar el inicio de sesión con SSO corporativo (OIDC)", "CORE", null, null });

            migrationBuilder.InsertData(
                table: "Core_RolPermisos",
                columns: new[] { "PermisoId", "RolId", "CreatedAt" },
                values: new object[,]
                {
                    { 422, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 423, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 424, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 422, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 423, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 424, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 422, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 423, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 425, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 425, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Core_Usuarios_SsoProveedor_SsoSubject",
                table: "Core_Usuarios",
                columns: new[] { "SsoProveedor", "SsoSubject" },
                unique: true,
                filter: "[SsoProveedor] IS NOT NULL AND [SsoSubject] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Core_EmpresaSso_DominioCorreo",
                table: "Core_EmpresaSso",
                column: "DominioCorreo");

            migrationBuilder.CreateIndex(
                name: "IX_Core_EmpresaSso_EmpresaId",
                table: "Core_EmpresaSso",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_EmpresaSso_RolPorDefectoId",
                table: "Core_EmpresaSso",
                column: "RolPorDefectoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Core_EmpresaSso");

            migrationBuilder.DropIndex(
                name: "IX_Core_Usuarios_SsoProveedor_SsoSubject",
                table: "Core_Usuarios");

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 422, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 423, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 424, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 425, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 422, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 423, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 424, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 425, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 422, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 423, 502 });

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 425);

            migrationBuilder.DropColumn(
                name: "SsoProveedor",
                table: "Core_Usuarios");

            migrationBuilder.DropColumn(
                name: "SsoSubject",
                table: "Core_Usuarios");
        }
    }
}
