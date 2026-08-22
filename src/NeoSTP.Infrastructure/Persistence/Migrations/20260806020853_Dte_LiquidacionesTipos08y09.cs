using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Dte_LiquidacionesTipos08y09 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LiquidacionCantidadDocumentos",
                table: "Dte_Documentos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiquidacionCodigo",
                table: "Dte_Documentos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiquidacionCodigoEmpleado",
                table: "Dte_Documentos",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LiquidacionComision",
                table: "Dte_Documentos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiquidacionDescripcionSinPercepcion",
                table: "Dte_Documentos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiquidacionDocumentoEntrega",
                table: "Dte_Documentos",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LiquidacionIvaComision",
                table: "Dte_Documentos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LiquidacionIvaPercibido",
                table: "Dte_Documentos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LiquidacionMontoSinPercepcion",
                table: "Dte_Documentos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiquidacionNombreEntrega",
                table: "Dte_Documentos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LiquidacionPeriodoFin",
                table: "Dte_Documentos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LiquidacionPeriodoInicio",
                table: "Dte_Documentos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LiquidacionPorcentajeComision",
                table: "Dte_Documentos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LiquidacionCantidadDocumentos",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionCodigo",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionCodigoEmpleado",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionComision",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionDescripcionSinPercepcion",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionDocumentoEntrega",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionIvaComision",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionIvaPercibido",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionMontoSinPercepcion",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionNombreEntrega",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionPeriodoFin",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionPeriodoInicio",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "LiquidacionPorcentajeComision",
                table: "Dte_Documentos");
        }
    }
}
