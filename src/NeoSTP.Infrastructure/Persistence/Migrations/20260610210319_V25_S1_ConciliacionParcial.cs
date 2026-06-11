using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V25_S1_ConciliacionParcial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tes_ConciliacionDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    MovimientoBancarioId = table.Column<int>(type: "int", nullable: false),
                    MovimientoTesoreriaId = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tes_ConciliacionDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tes_ConciliacionDetalles_Tes_MovimientosBanco_MovimientoBancarioId",
                        column: x => x.MovimientoBancarioId,
                        principalTable: "Tes_MovimientosBanco",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tes_ConciliacionDetalles_Tes_Movimientos_MovimientoTesoreriaId",
                        column: x => x.MovimientoTesoreriaId,
                        principalTable: "Tes_Movimientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tes_ConciliacionDetalles_EmpresaId_MovimientoBancarioId",
                table: "Tes_ConciliacionDetalles",
                columns: new[] { "EmpresaId", "MovimientoBancarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tes_ConciliacionDetalles_MovimientoBancarioId",
                table: "Tes_ConciliacionDetalles",
                column: "MovimientoBancarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Tes_ConciliacionDetalles_MovimientoTesoreriaId",
                table: "Tes_ConciliacionDetalles",
                column: "MovimientoTesoreriaId",
                unique: true);

            // Backfill: las conciliaciones 1:1 existentes (V2-D4) pasan al nuevo modelo de detalles.
            migrationBuilder.Sql("""
                INSERT INTO Tes_ConciliacionDetalles (EmpresaId, MovimientoBancarioId, MovimientoTesoreriaId, Monto, CreatedAt, CreatedBy)
                SELECT b.EmpresaId, b.Id, b.MovimientoTesoreriaId, t.Monto, ISNULL(b.ConciliadoAt, SYSUTCDATETIME()), b.ConciliadoPor
                FROM Tes_MovimientosBanco b
                INNER JOIN Tes_Movimientos t ON t.Id = b.MovimientoTesoreriaId
                WHERE b.EstadoCodigo = 'CONCILIADO' AND b.MovimientoTesoreriaId IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tes_ConciliacionDetalles");
        }
    }
}
