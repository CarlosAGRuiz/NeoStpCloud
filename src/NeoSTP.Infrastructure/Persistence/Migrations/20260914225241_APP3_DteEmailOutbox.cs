using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class APP3_DteEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntidadId",
                table: "Notif_Outbox",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntidadTipo",
                table: "Notif_Outbox",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Finalidad",
                table: "Notif_Outbox",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProveedorMessageId",
                table: "Notif_Outbox",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Outbox_EmpresaId_EntidadTipo_EntidadId_Finalidad_CreatedAt",
                table: "Notif_Outbox",
                columns: new[] { "EmpresaId", "EntidadTipo", "EntidadId", "Finalidad", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notif_Outbox_EmpresaId_EntidadTipo_EntidadId_Finalidad_CreatedAt",
                table: "Notif_Outbox");

            migrationBuilder.DropColumn(
                name: "EntidadId",
                table: "Notif_Outbox");

            migrationBuilder.DropColumn(
                name: "EntidadTipo",
                table: "Notif_Outbox");

            migrationBuilder.DropColumn(
                name: "Finalidad",
                table: "Notif_Outbox");

            migrationBuilder.DropColumn(
                name: "ProveedorMessageId",
                table: "Notif_Outbox");
        }
    }
}
