using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint14_PermisosCertificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 313, "Core.Certificacion.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver matriz y progreso de certificación DTE", "NEODTE", null, null },
                    { 314, "Core.Certificacion.Operar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Generar pruebas, asociar documentos, reintentar", "NEODTE", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_RolPermisos",
                columns: new[] { "PermisoId", "RolId", "CreatedAt" },
                values: new object[,]
                {
                    { 313, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 314, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 313, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 314, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 313, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 313, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 313, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 314, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 313, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 314, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 313, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 313, 503 });

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 313);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 314);
        }
    }
}
