using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2_PriceVatSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoPrecio",
                table: "Pos_VentaLineas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "IVA_INCLUIDO");

            migrationBuilder.AddColumn<string>(
                name: "TipoPrecio",
                table: "Dte_Productos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "IVA_INCLUIDO");

            migrationBuilder.AddColumn<string>(
                name: "Clasificacion",
                table: "Dte_DocumentoDetalles",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "GRAVADA");

            migrationBuilder.AddColumn<string>(
                name: "TipoPrecio",
                table: "Dte_DocumentoDetalles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AplicaIva",
                table: "Crm_CotizacionLineas",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoPrecio",
                table: "Crm_CotizacionLineas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "IVA_INCLUIDO");

            migrationBuilder.Sql(
                "UPDATE Crm_CotizacionLineas " +
                "SET AplicaIva = CASE WHEN VentaGravada > 0 THEN 1 ELSE 0 END;");

            migrationBuilder.Sql(
                "UPDATE Dte_DocumentoDetalles SET Clasificacion = CASE " +
                "WHEN VentaExenta > 0 THEN 'EXENTA' WHEN VentaNoSujeta > 0 OR NoGravado = 1 THEN 'NO_SUJETA' ELSE 'GRAVADA' END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoPrecio",
                table: "Pos_VentaLineas");

            migrationBuilder.DropColumn(
                name: "TipoPrecio",
                table: "Dte_Productos");

            migrationBuilder.DropColumn(
                name: "Clasificacion",
                table: "Dte_DocumentoDetalles");

            migrationBuilder.DropColumn(
                name: "TipoPrecio",
                table: "Dte_DocumentoDetalles");

            migrationBuilder.DropColumn(
                name: "AplicaIva",
                table: "Crm_CotizacionLineas");

            migrationBuilder.DropColumn(
                name: "TipoPrecio",
                table: "Crm_CotizacionLineas");
        }
    }
}
