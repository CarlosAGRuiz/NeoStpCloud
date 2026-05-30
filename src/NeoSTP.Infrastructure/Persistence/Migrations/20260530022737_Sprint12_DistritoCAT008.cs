using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint12_DistritoCAT008 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceptorDistritoCodigo",
                table: "Dte_Documentos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistritoCodigo",
                table: "Dte_Clientes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Distrito",
                table: "Core_Empresas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceptorDistritoCodigo",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "DistritoCodigo",
                table: "Dte_Clientes");

            migrationBuilder.DropColumn(
                name: "Distrito",
                table: "Core_Empresas");
        }
    }
}
