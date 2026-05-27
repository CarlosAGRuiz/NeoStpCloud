using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_ClientesYProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    TipoDocumentoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nrc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TipoContribuyenteCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodigoActividad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ActividadEconomica = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    DepartamentoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MunicipioCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_Clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_Clientes_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dte_Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    CodigoInterno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CodigoBarra = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TipoItem = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    AplicaIva = table.Column<bool>(type: "bit", nullable: false),
                    TributoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_Productos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Core_Catalogos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EmpresaId", "EsSistema", "Nombre", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 18, true, "DEPARTAMENTO_ES", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "14 departamentos de El Salvador con codigos MH CAT-012", null, true, "Departamento (El Salvador)", null, null });

            migrationBuilder.InsertData(
                table: "Core_CatalogoItems",
                columns: new[] { "Id", "Activo", "CatalogoId", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EsSistema", "MetadataJson", "Orden", "UpdatedAt", "UpdatedBy", "Valor" },
                values: new object[,]
                {
                    { 104, true, 18, "AHUACHAPAN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"01\"}", 1, null, null, "Ahuachapán" },
                    { 105, true, 18, "SANTA_ANA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"02\"}", 2, null, null, "Santa Ana" },
                    { 106, true, 18, "SONSONATE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"03\"}", 3, null, null, "Sonsonate" },
                    { 107, true, 18, "CHALATENANGO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"04\"}", 4, null, null, "Chalatenango" },
                    { 108, true, 18, "LA_LIBERTAD", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"05\"}", 5, null, null, "La Libertad" },
                    { 109, true, 18, "SAN_SALVADOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"06\"}", 6, null, null, "San Salvador" },
                    { 110, true, 18, "CUSCATLAN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"07\"}", 7, null, null, "Cuscatlán" },
                    { 111, true, 18, "LA_PAZ", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"08\"}", 8, null, null, "La Paz" },
                    { 112, true, 18, "CABANAS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"09\"}", 9, null, null, "Cabañas" },
                    { 113, true, 18, "SAN_VICENTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"10\"}", 10, null, null, "San Vicente" },
                    { 114, true, 18, "USULUTAN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"11\"}", 11, null, null, "Usulután" },
                    { 115, true, 18, "SAN_MIGUEL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"12\"}", 12, null, null, "San Miguel" },
                    { 116, true, 18, "MORAZAN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"13\"}", 13, null, null, "Morazán" },
                    { 117, true, 18, "LA_UNION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"14\"}", 14, null, null, "La Unión" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Clientes_EmpresaId_Nombre",
                table: "Dte_Clientes",
                columns: new[] { "EmpresaId", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Clientes_EmpresaId_TipoDocumentoCodigo_NumeroDocumento",
                table: "Dte_Clientes",
                columns: new[] { "EmpresaId", "TipoDocumentoCodigo", "NumeroDocumento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Clientes_EstadoCodigo",
                table: "Dte_Clientes",
                column: "EstadoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Productos_EmpresaId_CodigoBarra",
                table: "Dte_Productos",
                columns: new[] { "EmpresaId", "CodigoBarra" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Productos_EmpresaId_CodigoInterno",
                table: "Dte_Productos",
                columns: new[] { "EmpresaId", "CodigoInterno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Productos_EmpresaId_Nombre",
                table: "Dte_Productos",
                columns: new[] { "EmpresaId", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Productos_EstadoCodigo",
                table: "Dte_Productos",
                column: "EstadoCodigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_Clientes");

            migrationBuilder.DropTable(
                name: "Dte_Productos");

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 18);
        }
    }
}
