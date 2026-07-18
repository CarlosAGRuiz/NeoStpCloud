using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Mejora2_GastoNoDomiciliado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IvaImportacionMonto",
                table: "Profit_Gastos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ProveedorNoDomiciliado",
                table: "Profit_Gastos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RetencionRentaMonto",
                table: "Profit_Gastos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IvaImportacionMonto",
                table: "Profit_Gastos");

            migrationBuilder.DropColumn(
                name: "ProveedorNoDomiciliado",
                table: "Profit_Gastos");

            migrationBuilder.DropColumn(
                name: "RetencionRentaMonto",
                table: "Profit_Gastos");
        }
    }
}
