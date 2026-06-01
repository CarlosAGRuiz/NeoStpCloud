using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint15_CertificacionPruebaEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventoId",
                table: "Dte_CertificacionPruebas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_CertificacionPruebas_EventoId",
                table: "Dte_CertificacionPruebas",
                column: "EventoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dte_CertificacionPruebas_Dte_Eventos_EventoId",
                table: "Dte_CertificacionPruebas",
                column: "EventoId",
                principalTable: "Dte_Eventos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dte_CertificacionPruebas_Dte_Eventos_EventoId",
                table: "Dte_CertificacionPruebas");

            migrationBuilder.DropIndex(
                name: "IX_Dte_CertificacionPruebas_EventoId",
                table: "Dte_CertificacionPruebas");

            migrationBuilder.DropColumn(
                name: "EventoId",
                table: "Dte_CertificacionPruebas");
        }
    }
}
