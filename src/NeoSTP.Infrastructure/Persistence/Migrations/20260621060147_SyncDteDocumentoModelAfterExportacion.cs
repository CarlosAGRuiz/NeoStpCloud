using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncDteDocumentoModelAfterExportacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
    ALTER TABLE DBO.Dte_Documentos
    ALTER COLUMN FormaPagoCodigo NVARCHAR(50) NULL;
    """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
    ALTER TABLE DBO.Dte_Documentos
    ALTER COLUMN FormaPagoCodigo NVARCHAR(10) NULL;
    """);
        }

    }
}
