using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Branding_LogoFirmaEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "FirmaBlob",
                table: "Core_Empresas",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmaContentType",
                table: "Core_Empresas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmaTexto",
                table: "Core_Empresas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoBlob",
                table: "Core_Empresas",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Core_Empresas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirmaBlob",
                table: "Core_Empresas");

            migrationBuilder.DropColumn(
                name: "FirmaContentType",
                table: "Core_Empresas");

            migrationBuilder.DropColumn(
                name: "FirmaTexto",
                table: "Core_Empresas");

            migrationBuilder.DropColumn(
                name: "LogoBlob",
                table: "Core_Empresas");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Core_Empresas");
        }
    }
}
