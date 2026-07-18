using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Entrega6_NeoAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ComisionPorcentaje",
                table: "Rrhh_Empleados",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Agenda_Citas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ClienteNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: true),
                    EmpleadoNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ServicioProductoId = table.Column<int>(type: "int", nullable: true),
                    ServicioNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "int", nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agenda_Citas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agenda_Citas_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Core_Modulos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Icono", "Nombre", "Orden", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 117, true, "NEOAGENDA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Citas, calendario y comisiones por servicio", "calendar_month", "NeoAgenda", 19, null, null });

            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 422, "Agenda.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver calendario de citas y comisiones", "NEOAGENDA", null, null },
                    { 423, "Agenda.Gestionar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear, reprogramar y cambiar estado de citas", "NEOAGENDA", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_PlanModulos",
                columns: new[] { "ModuloId", "PlanId", "Activo", "CreatedAt" },
                values: new object[,]
                {
                    { 117, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 117, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agenda_Citas_EmpresaId_EmpleadoId_FechaInicio",
                table: "Agenda_Citas",
                columns: new[] { "EmpresaId", "EmpleadoId", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Agenda_Citas_EmpresaId_EstadoCodigo",
                table: "Agenda_Citas",
                columns: new[] { "EmpresaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Agenda_Citas_EmpresaId_FechaInicio",
                table: "Agenda_Citas",
                columns: new[] { "EmpresaId", "FechaInicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agenda_Citas");

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 422);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 423);

            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 117, 203 });

            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 117, 204 });

            migrationBuilder.DeleteData(
                table: "Core_Modulos",
                keyColumn: "Id",
                keyValue: 117);

            migrationBuilder.DropColumn(
                name: "ComisionPorcentaje",
                table: "Rrhh_Empleados");
        }
    }
}
