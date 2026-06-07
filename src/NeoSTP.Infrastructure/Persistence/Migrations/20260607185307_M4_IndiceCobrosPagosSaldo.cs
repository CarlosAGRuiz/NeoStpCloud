using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4_IndiceCobrosPagosSaldo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cobros_Pagos_DteDocumentoId",
                table: "Cobros_Pagos");

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_Pagos_DteDocumentoId_EstadoCodigo",
                table: "Cobros_Pagos",
                columns: new[] { "DteDocumentoId", "EstadoCodigo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cobros_Pagos_DteDocumentoId_EstadoCodigo",
                table: "Cobros_Pagos");

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_Pagos_DteDocumentoId",
                table: "Cobros_Pagos",
                column: "DteDocumentoId");
        }
    }
}
