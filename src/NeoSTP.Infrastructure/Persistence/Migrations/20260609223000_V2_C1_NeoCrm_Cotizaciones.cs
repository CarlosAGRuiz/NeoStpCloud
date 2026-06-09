using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NeoStpDbContext))]
    [Migration("20260609223000_V2_C1_NeoCrm_Cotizaciones")]
    public partial class V2_C1_NeoCrm_Cotizaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Crm_Cotizaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    OportunidadCrmId = table.Column<int>(type: "int", nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ContactoCrmId = table.Column<int>(type: "int", nullable: true),
                    DteDocumentoId = table.Column<int>(type: "int", nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaValidez = table.Column<DateOnly>(type: "date", nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MonedaCodigo = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DescuentoTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IvaTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Terminos = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crm_Cotizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crm_Cotizaciones_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Cotizaciones_Crm_Contactos_ContactoCrmId",
                        column: x => x.ContactoCrmId,
                        principalTable: "Crm_Contactos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Cotizaciones_Crm_Oportunidades_OportunidadCrmId",
                        column: x => x.OportunidadCrmId,
                        principalTable: "Crm_Oportunidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Cotizaciones_Dte_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Dte_Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Cotizaciones_Dte_Documentos_DteDocumentoId",
                        column: x => x.DteDocumentoId,
                        principalTable: "Dte_Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Crm_CotizacionLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    CotizacionCrmId = table.Column<int>(type: "int", nullable: false),
                    NumeroLinea = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    TipoItem = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PorcentajeDescuento = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    MontoDescuento = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VentaNoSujeta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VentaExenta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VentaGravada = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IvaItem = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalLinea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crm_CotizacionLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crm_CotizacionLineas_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_CotizacionLineas_Crm_Cotizaciones_CotizacionCrmId",
                        column: x => x.CotizacionCrmId,
                        principalTable: "Crm_Cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Crm_CotizacionLineas_Dte_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Dte_Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_CotizacionLineas_CotizacionCrmId",
                table: "Crm_CotizacionLineas",
                column: "CotizacionCrmId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_CotizacionLineas_EmpresaId_CotizacionCrmId",
                table: "Crm_CotizacionLineas",
                columns: new[] { "EmpresaId", "CotizacionCrmId" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_CotizacionLineas_ProductoId",
                table: "Crm_CotizacionLineas",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Cotizaciones_ClienteId",
                table: "Crm_Cotizaciones",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Cotizaciones_ContactoCrmId",
                table: "Crm_Cotizaciones",
                column: "ContactoCrmId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Cotizaciones_DteDocumentoId",
                table: "Crm_Cotizaciones",
                column: "DteDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Cotizaciones_EmpresaId_EstadoCodigo_FechaEmision",
                table: "Crm_Cotizaciones",
                columns: new[] { "EmpresaId", "EstadoCodigo", "FechaEmision" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Cotizaciones_EmpresaId_Numero",
                table: "Crm_Cotizaciones",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Cotizaciones_EmpresaId_OportunidadCrmId",
                table: "Crm_Cotizaciones",
                columns: new[] { "EmpresaId", "OportunidadCrmId" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Cotizaciones_OportunidadCrmId",
                table: "Crm_Cotizaciones",
                column: "OportunidadCrmId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Crm_CotizacionLineas");

            migrationBuilder.DropTable(
                name: "Crm_Cotizaciones");
        }
    }
}
