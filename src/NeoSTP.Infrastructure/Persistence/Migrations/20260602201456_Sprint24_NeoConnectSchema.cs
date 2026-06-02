using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint24_NeoConnectSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Connect_ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoUsoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connect_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Connect_Webhooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ApiKeyId = table.Column<int>(type: "int", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SecretoHmac = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Eventos = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    UltimaEntregaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connect_Webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Connect_Webhooks_Connect_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "Connect_ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Connect_WebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WebhookId = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Evento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HttpStatus = table.Column<int>(type: "int", nullable: true),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    ProximoIntento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntregadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connect_WebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Connect_WebhookDeliveries_Connect_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Connect_Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 351, "Connect.ApiKeys.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Consultar API Keys de integración", "NEOCONNECT", null, null },
                    { 352, "Connect.ApiKeys.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear, revocar y gestionar API Keys", "NEOCONNECT", null, null },
                    { 353, "Connect.Webhooks.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Consultar webhooks configurados", "NEOCONNECT", null, null },
                    { 354, "Connect.Webhooks.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear, editar y eliminar webhooks", "NEOCONNECT", null, null },
                    { 355, "Connect.Logs.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver logs de uso y entregas de webhooks", "NEOCONNECT", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Connect_ApiKeys_EmpresaId_Activo",
                table: "Connect_ApiKeys",
                columns: new[] { "EmpresaId", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_Connect_ApiKeys_KeyHash",
                table: "Connect_ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Connect_WebhookDeliveries_EmpresaId_CreatedAt",
                table: "Connect_WebhookDeliveries",
                columns: new[] { "EmpresaId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Connect_WebhookDeliveries_Estado_ProximoIntento",
                table: "Connect_WebhookDeliveries",
                columns: new[] { "Estado", "ProximoIntento" });

            migrationBuilder.CreateIndex(
                name: "IX_Connect_WebhookDeliveries_WebhookId",
                table: "Connect_WebhookDeliveries",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_Connect_Webhooks_ApiKeyId",
                table: "Connect_Webhooks",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_Connect_Webhooks_EmpresaId_Activo",
                table: "Connect_Webhooks",
                columns: new[] { "EmpresaId", "Activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Connect_WebhookDeliveries");

            migrationBuilder.DropTable(
                name: "Connect_Webhooks");

            migrationBuilder.DropTable(
                name: "Connect_ApiKeys");

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 351);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 352);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 353);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 354);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 355);
        }
    }
}
