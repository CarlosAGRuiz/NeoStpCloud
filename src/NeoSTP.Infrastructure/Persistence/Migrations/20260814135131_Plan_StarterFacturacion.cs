using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Plan_StarterFacturacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Core_Planes",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "LimiteDteMensual", "LimitePuntosVenta", "LimiteSucursales", "LimiteUsuarios", "MonedaCodigo", "Nombre", "PrecioMensual", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 207, true, "STARTERFE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Facturación electrónica con 3 usuarios", 100, 2, 1, 3, "USD", "Starter Facturación", 15m, null, null });

            migrationBuilder.InsertData(
                table: "Core_PlanModulos",
                columns: new[] { "ModuloId", "PlanId", "Activo", "CreatedAt" },
                values: new object[,]
                {
                    { 100, 207, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, 207, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 100, 207 });

            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 101, 207 });

            migrationBuilder.DeleteData(
                table: "Core_Planes",
                keyColumn: "Id",
                keyValue: 207);
        }
    }
}
