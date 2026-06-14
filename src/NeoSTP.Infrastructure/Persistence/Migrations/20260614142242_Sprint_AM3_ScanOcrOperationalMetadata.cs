using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint_AM3_ScanOcrOperationalMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OcrDuracionMs",
                table: "Scan_Documentos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrErrorResumen",
                table: "Scan_Documentos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OcrIntentos",
                table: "Scan_Documentos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OcrModelo",
                table: "Scan_Documentos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrProveedor",
                table: "Scan_Documentos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OcrUltimoIntentoAt",
                table: "Scan_Documentos",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OcrDuracionMs",
                table: "Scan_Documentos");

            migrationBuilder.DropColumn(
                name: "OcrErrorResumen",
                table: "Scan_Documentos");

            migrationBuilder.DropColumn(
                name: "OcrIntentos",
                table: "Scan_Documentos");

            migrationBuilder.DropColumn(
                name: "OcrModelo",
                table: "Scan_Documentos");

            migrationBuilder.DropColumn(
                name: "OcrProveedor",
                table: "Scan_Documentos");

            migrationBuilder.DropColumn(
                name: "OcrUltimoIntentoAt",
                table: "Scan_Documentos");
        }
    }
}
