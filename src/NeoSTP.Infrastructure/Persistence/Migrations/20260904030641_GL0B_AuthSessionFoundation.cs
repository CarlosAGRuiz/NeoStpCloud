using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GL0B_AuthSessionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "Core_Usuarios",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Core_RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Core_AuthSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CredentialFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AuthorizationFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_AuthSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_AuthSessions_Core_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Core_Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Core_RefreshTokens_SessionId",
                table: "Core_RefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_AuthSessions_UsuarioId_ExpiresAt",
                table: "Core_AuthSessions",
                columns: new[] { "UsuarioId", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Core_RefreshTokens_Core_AuthSessions_SessionId",
                table: "Core_RefreshTokens",
                column: "SessionId",
                principalTable: "Core_AuthSessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Core_RefreshTokens_Core_AuthSessions_SessionId",
                table: "Core_RefreshTokens");

            migrationBuilder.DropTable(
                name: "Core_AuthSessions");

            migrationBuilder.DropIndex(
                name: "IX_Core_RefreshTokens_SessionId",
                table: "Core_RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Core_Usuarios");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Core_RefreshTokens");
        }
    }
}
