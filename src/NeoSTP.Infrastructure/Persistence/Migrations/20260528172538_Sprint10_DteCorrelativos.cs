using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint10_DteCorrelativos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_Correlativos",
                columns: table => new
                {
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    TipoDteCodigo = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UltimoCorrelativo = table.Column<int>(type: "int", nullable: false),
                    ActualizadoAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_Correlativos", x => new { x.EmpresaId, x.TipoDteCodigo });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_Correlativos");
        }
    }
}
