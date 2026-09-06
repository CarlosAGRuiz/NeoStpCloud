using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GL0C_SsoIdentityMfaConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Core_Usuarios_SsoProveedor_SsoSubject",
                table: "Core_Usuarios");

            migrationBuilder.AddColumn<Guid>(
                name: "MfaVersion",
                table: "Core_Usuarios",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SsoIssuer",
                table: "Core_Usuarios",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_Usuarios_SsoProveedor_SsoIssuer_SsoSubject",
                table: "Core_Usuarios",
                columns: new[] { "SsoProveedor", "SsoIssuer", "SsoSubject" },
                unique: true,
                filter: "[SsoProveedor] IS NOT NULL AND [SsoIssuer] IS NOT NULL AND [SsoSubject] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Core_Usuarios_SsoProveedor_SsoIssuer_SsoSubject",
                table: "Core_Usuarios");

            migrationBuilder.DropColumn(
                name: "MfaVersion",
                table: "Core_Usuarios");

            migrationBuilder.DropColumn(
                name: "SsoIssuer",
                table: "Core_Usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Usuarios_SsoProveedor_SsoSubject",
                table: "Core_Usuarios",
                columns: new[] { "SsoProveedor", "SsoSubject" },
                unique: true,
                filter: "[SsoProveedor] IS NOT NULL AND [SsoSubject] IS NOT NULL");
        }
    }
}
