using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint18_LegalConsentimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Core_UserConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    ConsentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedFromIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    AcceptedUserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_UserConsents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Core_UserConsents_AcceptedAt",
                table: "Core_UserConsents",
                column: "AcceptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Core_UserConsents_UsuarioId_ConsentType_Version",
                table: "Core_UserConsents",
                columns: new[] { "UsuarioId", "ConsentType", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Core_UserConsents");
        }
    }
}
