using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V3_S2_RecepcionesOrdenCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Compras_OrdenRecepciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    OrdenCompraId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compras_OrdenRecepciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenRecepciones_Compras_Ordenes_OrdenCompraId",
                        column: x => x.OrdenCompraId,
                        principalTable: "Compras_Ordenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenRecepciones_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Compras_OrdenRecepcionLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    OrdenCompraRecepcionId = table.Column<int>(type: "int", nullable: false),
                    OrdenCompraLineaId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MovimientoInventarioId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compras_OrdenRecepcionLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenRecepcionLineas_Compras_OrdenLineas_OrdenCompraLineaId",
                        column: x => x.OrdenCompraLineaId,
                        principalTable: "Compras_OrdenLineas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenRecepcionLineas_Compras_OrdenRecepciones_OrdenCompraRecepcionId",
                        column: x => x.OrdenCompraRecepcionId,
                        principalTable: "Compras_OrdenRecepciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenRecepcionLineas_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Compras_OrdenRecepcionLineas_Inv_Movimientos_MovimientoInventarioId",
                        column: x => x.MovimientoInventarioId,
                        principalTable: "Inv_Movimientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenRecepciones_EmpresaId_IdempotencyKey",
                table: "Compras_OrdenRecepciones",
                columns: new[] { "EmpresaId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenRecepciones_EmpresaId_Numero",
                table: "Compras_OrdenRecepciones",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenRecepciones_EmpresaId_OrdenCompraId_Fecha",
                table: "Compras_OrdenRecepciones",
                columns: new[] { "EmpresaId", "OrdenCompraId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenRecepciones_OrdenCompraId",
                table: "Compras_OrdenRecepciones",
                column: "OrdenCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenRecepcionLineas_EmpresaId",
                table: "Compras_OrdenRecepcionLineas",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenRecepcionLineas_MovimientoInventarioId",
                table: "Compras_OrdenRecepcionLineas",
                column: "MovimientoInventarioId",
                unique: true,
                filter: "[MovimientoInventarioId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenRecepcionLineas_OrdenCompraLineaId",
                table: "Compras_OrdenRecepcionLineas",
                column: "OrdenCompraLineaId");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_OrdenRecepcionLineas_OrdenCompraRecepcionId_OrdenCompraLineaId",
                table: "Compras_OrdenRecepcionLineas",
                columns: new[] { "OrdenCompraRecepcionId", "OrdenCompraLineaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Compras_OrdenRecepcionLineas");

            migrationBuilder.DropTable(
                name: "Compras_OrdenRecepciones");
        }
    }
}
