using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PagosLatam_MetodosPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComprobanteUrl",
                table: "Billing_Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metodo",
                table: "Billing_Payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificadoAt",
                table: "Billing_Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificadoPor",
                table: "Billing_Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComprobanteUrl",
                table: "Billing_Payments");

            migrationBuilder.DropColumn(
                name: "Metodo",
                table: "Billing_Payments");

            migrationBuilder.DropColumn(
                name: "VerificadoAt",
                table: "Billing_Payments");

            migrationBuilder.DropColumn(
                name: "VerificadoPor",
                table: "Billing_Payments");
        }
    }
}
