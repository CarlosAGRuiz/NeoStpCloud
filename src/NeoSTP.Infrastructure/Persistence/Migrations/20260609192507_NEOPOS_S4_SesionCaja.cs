using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NEOPOS_S4_SesionCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SesionCajaId",
                table: "Pos_Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Pos_SesionesCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: true),
                    PuntoVentaId = table.Column<int>(type: "int", nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AbiertaAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MontoInicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AbiertaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CerradaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MontoEsperado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MontoContado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Diferencia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CerradaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pos_SesionesCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pos_SesionesCaja_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pos_Ventas_SesionCajaId",
                table: "Pos_Ventas",
                column: "SesionCajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pos_SesionesCaja_EmpresaId_EstadoCodigo",
                table: "Pos_SesionesCaja",
                columns: new[] { "EmpresaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Pos_SesionesCaja_EmpresaId_Numero",
                table: "Pos_SesionesCaja",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pos_SesionesCaja");

            migrationBuilder.DropIndex(
                name: "IX_Pos_Ventas_SesionCajaId",
                table: "Pos_Ventas");

            migrationBuilder.DropColumn(
                name: "SesionCajaId",
                table: "Pos_Ventas");
        }
    }
}
