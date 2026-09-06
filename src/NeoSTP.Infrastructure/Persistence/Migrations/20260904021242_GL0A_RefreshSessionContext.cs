using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GL0A_RefreshSessionContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContextEmpresaId",
                table: "Core_RefreshTokens",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContextInitialized",
                table: "Core_RefreshTokens",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextEmpresaId",
                table: "Core_RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ContextInitialized",
                table: "Core_RefreshTokens");
        }
    }
}
