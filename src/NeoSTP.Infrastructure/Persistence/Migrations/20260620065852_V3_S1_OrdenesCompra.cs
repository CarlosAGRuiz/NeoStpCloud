using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V3_S1_OrdenesCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Compras_Ordenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaEntregaEsperada = table.Column<DateOnly>(type: "date", nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MonedaCodigo = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Iva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FacturaCompraId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compras_Ordenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compras_Ordenes_Compras_Facturas_FacturaCompraId",
                        column: x => x.FacturaCompraId,
                        principalTable: "Compras_Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Compras_Ordenes_Compras_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Compras_Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Compras_Ordenes_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Compras_OrdenLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    OrdenCompraId = table.Column<int>(type: "int", nullable: false),
                    NumeroLinea = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AplicaIva = table.Column<bool>(type: "bit", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Iva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compras_OrdenLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenLineas_Compras_Ordenes_OrdenCompraId",
                        column: x => x.OrdenCompraId,
                        principalTable: "Compras_Ordenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenLineas_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenLineas_Dte_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Dte_Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Compras_Ordenes_EmpresaId_EstadoCodigo_Fecha",
                table: "Compras_Ordenes",
                columns: new[] { "EmpresaId", "EstadoCodigo", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Compras_Ordenes_EmpresaId_Numero",
                table: "Compras_Ordenes",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Compras_Ordenes_EmpresaId_ProveedorId",
                table: "Compras_Ordenes",
                columns: new[] { "EmpresaId", "ProveedorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Compras_Ordenes_FacturaCompraId",
                table: "Compras_Ordenes",
                column: "FacturaCompraId",
                unique: true,
                filter: "[FacturaCompraId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_Ordenes_ProveedorId",
                table: "Compras_Ordenes",
                column: "ProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenLineas_EmpresaId_OrdenCompraId",
                table: "Compras_OrdenLineas",
                columns: new[] { "EmpresaId", "OrdenCompraId" });

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenLineas_OrdenCompraId_NumeroLinea",
                table: "Compras_OrdenLineas",
                columns: new[] { "OrdenCompraId", "NumeroLinea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenLineas_ProductoId",
                table: "Compras_OrdenLineas",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Compras_OrdenLineas");

            migrationBuilder.DropTable(
                name: "Compras_Ordenes");
        }
    }
}
