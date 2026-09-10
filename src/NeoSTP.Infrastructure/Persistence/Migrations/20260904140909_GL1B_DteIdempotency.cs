using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GL1B_DteIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKeyHash",
                table: "Dte_Documentos",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyRequestHash",
                table: "Dte_Documentos",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyScope",
                table: "Dte_Documentos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Documentos_EmpresaId_IdempotencyScope_IdempotencyKeyHash",
                table: "Dte_Documentos",
                columns: new[] { "EmpresaId", "IdempotencyScope", "IdempotencyKeyHash" },
                unique: true,
                filter: "[IdempotencyKeyHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dte_Documentos_EmpresaId_IdempotencyScope_IdempotencyKeyHash",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "IdempotencyKeyHash",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "IdempotencyRequestHash",
                table: "Dte_Documentos");

            migrationBuilder.DropColumn(
                name: "IdempotencyScope",
                table: "Dte_Documentos");
        }
    }
}
