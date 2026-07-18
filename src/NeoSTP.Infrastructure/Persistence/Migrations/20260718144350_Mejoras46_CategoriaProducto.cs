using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Mejoras46_CategoriaProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoriaCodigo",
                table: "Dte_Productos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Productos_EmpresaId_CategoriaCodigo",
                table: "Dte_Productos",
                columns: new[] { "EmpresaId", "CategoriaCodigo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dte_Productos_EmpresaId_CategoriaCodigo",
                table: "Dte_Productos");

            migrationBuilder.DropColumn(
                name: "CategoriaCodigo",
                table: "Dte_Productos");
        }
    }
}
