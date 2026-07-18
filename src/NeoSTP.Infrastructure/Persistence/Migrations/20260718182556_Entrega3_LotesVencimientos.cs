using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Entrega3_LotesVencimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroLote",
                table: "Inv_Movimientos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ControlaLote",
                table: "Dte_Productos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Inv_Lotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    NumeroLote = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_Lotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_Lotes_Dte_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Dte_Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Lotes_EmpresaId_FechaVencimiento",
                table: "Inv_Lotes",
                columns: new[] { "EmpresaId", "FechaVencimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Lotes_EmpresaId_ProductoId_NumeroLote",
                table: "Inv_Lotes",
                columns: new[] { "EmpresaId", "ProductoId", "NumeroLote" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Lotes_ProductoId",
                table: "Inv_Lotes",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inv_Lotes");

            migrationBuilder.DropColumn(
                name: "NumeroLote",
                table: "Inv_Movimientos");

            migrationBuilder.DropColumn(
                name: "ControlaLote",
                table: "Dte_Productos");
        }
    }
}
