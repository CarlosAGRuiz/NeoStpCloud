using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint13_PermisosCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 311, "Core.Catalogos.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Consultar catálogos", "CORE", null, null },
                    { 312, "Core.Catalogos.Importar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Importar / exportar catálogos", "CORE", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_RolPermisos",
                columns: new[] { "PermisoId", "RolId", "CreatedAt" },
                values: new object[,]
                {
                    { 311, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 312, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 311, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 312, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 311, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 311, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 311, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 311, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 312, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 311, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 312, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 311, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 311, 503 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 311, 504 });

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 311);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 312);
        }
    }
}
