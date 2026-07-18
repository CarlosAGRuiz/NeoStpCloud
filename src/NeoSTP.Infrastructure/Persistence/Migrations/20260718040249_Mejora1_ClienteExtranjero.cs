using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Mejora1_ClienteExtranjero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dte_Clientes_EmpresaId_TipoDocumentoCodigo_NumeroDocumento",
                table: "Dte_Clientes");

            migrationBuilder.AlterColumn<string>(
                name: "NumeroDocumento",
                table: "Dte_Clientes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "PaisCodigo",
                table: "Dte_Clientes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoPersona",
                table: "Dte_Clientes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Clientes_EmpresaId_TipoDocumentoCodigo_NumeroDocumento",
                table: "Dte_Clientes",
                columns: new[] { "EmpresaId", "TipoDocumentoCodigo", "NumeroDocumento" },
                unique: true,
                filter: "[NumeroDocumento] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dte_Clientes_EmpresaId_TipoDocumentoCodigo_NumeroDocumento",
                table: "Dte_Clientes");

            migrationBuilder.DropColumn(
                name: "PaisCodigo",
                table: "Dte_Clientes");

            migrationBuilder.DropColumn(
                name: "TipoPersona",
                table: "Dte_Clientes");

            migrationBuilder.AlterColumn<string>(
                name: "NumeroDocumento",
                table: "Dte_Clientes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Clientes_EmpresaId_TipoDocumentoCodigo_NumeroDocumento",
                table: "Dte_Clientes",
                columns: new[] { "EmpresaId", "TipoDocumentoCodigo", "NumeroDocumento" },
                unique: true);
        }
    }
}
