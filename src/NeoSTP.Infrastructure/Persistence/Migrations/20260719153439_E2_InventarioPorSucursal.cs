using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class E2_InventarioPorSucursal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_Lotes_EmpresaId_ProductoId_NumeroLote",
                table: "Inv_Lotes");

            migrationBuilder.DropIndex(
                name: "IX_Inv_Existencias_EmpresaId_ProductoId",
                table: "Inv_Existencias");

            migrationBuilder.AddColumn<int>(
                name: "SucursalId",
                table: "Inv_Movimientos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SucursalId",
                table: "Inv_Lotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SucursalId",
                table: "Inv_Existencias",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Lotes_EmpresaId_ProductoId_SucursalId_NumeroLote",
                table: "Inv_Lotes",
                columns: new[] { "EmpresaId", "ProductoId", "SucursalId", "NumeroLote" },
                unique: true,
                filter: "[SucursalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Existencias_EmpresaId_ProductoId_SucursalId",
                table: "Inv_Existencias",
                columns: new[] { "EmpresaId", "ProductoId", "SucursalId" },
                unique: true,
                filter: "[SucursalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_Lotes_EmpresaId_ProductoId_SucursalId_NumeroLote",
                table: "Inv_Lotes");

            migrationBuilder.DropIndex(
                name: "IX_Inv_Existencias_EmpresaId_ProductoId_SucursalId",
                table: "Inv_Existencias");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Inv_Movimientos");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Inv_Lotes");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Inv_Existencias");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Lotes_EmpresaId_ProductoId_NumeroLote",
                table: "Inv_Lotes",
                columns: new[] { "EmpresaId", "ProductoId", "NumeroLote" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Existencias_EmpresaId_ProductoId",
                table: "Inv_Existencias",
                columns: new[] { "EmpresaId", "ProductoId" },
                unique: true);
        }
    }
}
