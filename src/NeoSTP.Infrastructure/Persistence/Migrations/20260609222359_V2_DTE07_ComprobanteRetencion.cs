using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V2_DTE07_ComprobanteRetencion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DocRelacionadoFecha",
                table: "Dte_DocumentoDetalles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocRelacionadoTipoDte",
                table: "Dte_DocumentoDetalles",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetencionCodigoMH",
                table: "Dte_DocumentoDetalles",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocRelacionadoFecha",
                table: "Dte_DocumentoDetalles");

            migrationBuilder.DropColumn(
                name: "DocRelacionadoTipoDte",
                table: "Dte_DocumentoDetalles");

            migrationBuilder.DropColumn(
                name: "RetencionCodigoMH",
                table: "Dte_DocumentoDetalles");
        }
    }
}
