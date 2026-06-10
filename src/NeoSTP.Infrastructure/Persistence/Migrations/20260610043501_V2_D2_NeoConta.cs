using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V2_D2_NeoConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Conta_Asientos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrigenId = table.Column<int>(type: "int", nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReversaDeId = table.Column<int>(type: "int", nullable: true),
                    TotalDebe = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalHaber = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conta_Asientos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conta_Asientos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conta_Cuentas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conta_Cuentas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conta_Cuentas_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conta_AsientoLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsientoContableId = table.Column<int>(type: "int", nullable: false),
                    CuentaContableId = table.Column<int>(type: "int", nullable: false),
                    Debe = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Haber = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conta_AsientoLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conta_AsientoLineas_Conta_Asientos_AsientoContableId",
                        column: x => x.AsientoContableId,
                        principalTable: "Conta_Asientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Conta_AsientoLineas_Conta_Cuentas_CuentaContableId",
                        column: x => x.CuentaContableId,
                        principalTable: "Conta_Cuentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Core_Modulos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Icono", "Nombre", "Orden", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 116, true, "NEOCONTA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Asientos contables básicos y balanza", "book_open", "NeoConta", 18, null, null });

            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 418, "Conta.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver asientos contables y balanza", "NEOCONTA", null, null },
                    { 419, "Conta.Gestionar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Generar asientos automáticos y reversar", "NEOCONTA", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_PlanModulos",
                columns: new[] { "ModuloId", "PlanId", "Activo", "CreatedAt" },
                values: new object[,]
                {
                    { 116, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 116, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Core_RolPermisos",
                columns: new[] { "PermisoId", "RolId", "CreatedAt" },
                values: new object[,]
                {
                    { 418, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 419, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 418, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 419, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 418, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 419, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 418, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 418, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conta_AsientoLineas_AsientoContableId",
                table: "Conta_AsientoLineas",
                column: "AsientoContableId");

            migrationBuilder.CreateIndex(
                name: "IX_Conta_AsientoLineas_CuentaContableId",
                table: "Conta_AsientoLineas",
                column: "CuentaContableId");

            migrationBuilder.CreateIndex(
                name: "IX_Conta_Asientos_EmpresaId_Fecha",
                table: "Conta_Asientos",
                columns: new[] { "EmpresaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Conta_Asientos_EmpresaId_Numero",
                table: "Conta_Asientos",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conta_Asientos_EmpresaId_Origen_OrigenId",
                table: "Conta_Asientos",
                columns: new[] { "EmpresaId", "Origen", "OrigenId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conta_Cuentas_EmpresaId_Codigo",
                table: "Conta_Cuentas",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Conta_AsientoLineas");

            migrationBuilder.DropTable(
                name: "Conta_Asientos");

            migrationBuilder.DropTable(
                name: "Conta_Cuentas");

            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 116, 203 });

            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 116, 204 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 418, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 419, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 418, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 419, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 418, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 419, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 418, 503 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 418, 504 });

            migrationBuilder.DeleteData(
                table: "Core_Modulos",
                keyColumn: "Id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 418);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 419);
        }
    }
}
