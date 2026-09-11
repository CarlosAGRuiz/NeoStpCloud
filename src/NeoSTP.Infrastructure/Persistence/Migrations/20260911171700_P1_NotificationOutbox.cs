using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P1_NotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notif_Outbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Canal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Destinatario = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClaveIdempotencia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    MaxIntentos = table.Column<int>(type: "int", nullable: false),
                    DisponibleDesde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcesadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorUltimo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LeaseId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notif_Outbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notif_Outbox_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Outbox_EmpresaId_ClaveIdempotencia",
                table: "Notif_Outbox",
                columns: new[] { "EmpresaId", "ClaveIdempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Outbox_EmpresaId_CreatedAt",
                table: "Notif_Outbox",
                columns: new[] { "EmpresaId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Outbox_Estado_DisponibleDesde_LeaseExpiresAt",
                table: "Notif_Outbox",
                columns: new[] { "Estado", "DisponibleDesde", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notif_Outbox");
        }
    }
}
