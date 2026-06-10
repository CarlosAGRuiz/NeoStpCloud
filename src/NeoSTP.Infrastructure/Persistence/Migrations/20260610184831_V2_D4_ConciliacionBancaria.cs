using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V2_D4_ConciliacionBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tes_MovimientosBanco",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    CuentaTesoreriaId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MovimientoTesoreriaId = table.Column<int>(type: "int", nullable: true),
                    ConciliadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConciliadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tes_MovimientosBanco", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tes_MovimientosBanco_Tes_Cuentas_CuentaTesoreriaId",
                        column: x => x.CuentaTesoreriaId,
                        principalTable: "Tes_Cuentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tes_MovimientosBanco_Tes_Movimientos_MovimientoTesoreriaId",
                        column: x => x.MovimientoTesoreriaId,
                        principalTable: "Tes_Movimientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tes_MovimientosBanco_CuentaTesoreriaId",
                table: "Tes_MovimientosBanco",
                column: "CuentaTesoreriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tes_MovimientosBanco_EmpresaId_CuentaTesoreriaId_EstadoCodigo",
                table: "Tes_MovimientosBanco",
                columns: new[] { "EmpresaId", "CuentaTesoreriaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Tes_MovimientosBanco_EmpresaId_CuentaTesoreriaId_Fecha",
                table: "Tes_MovimientosBanco",
                columns: new[] { "EmpresaId", "CuentaTesoreriaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Tes_MovimientosBanco_MovimientoTesoreriaId",
                table: "Tes_MovimientosBanco",
                column: "MovimientoTesoreriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tes_MovimientosBanco");
        }
    }
}
