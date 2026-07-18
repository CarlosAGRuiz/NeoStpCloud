using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceptorPaisExportacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
    IF COL_LENGTH('DBO.Dte_Documentos', 'ReceptorPaisCodigo') IS NULL
    BEGIN
        ALTER TABLE DBO.Dte_Documentos ADD ReceptorPaisCodigo NVARCHAR(10) NULL;
    END;

    IF COL_LENGTH('DBO.Dte_Documentos', 'ReceptorPaisNombre') IS NULL
    BEGIN
        ALTER TABLE DBO.Dte_Documentos ADD ReceptorPaisNombre NVARCHAR(100) NULL;
    END;

    IF COL_LENGTH('DBO.Dte_Documentos', 'ReceptorTipoPersona') IS NULL
    BEGIN
        ALTER TABLE DBO.Dte_Documentos ADD ReceptorTipoPersona INT NULL;
    END;
    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
    IF COL_LENGTH('DBO.Dte_Documentos', 'ReceptorTipoPersona') IS NOT NULL
    BEGIN
        ALTER TABLE DBO.Dte_Documentos DROP COLUMN ReceptorTipoPersona;
    END;

    IF COL_LENGTH('DBO.Dte_Documentos', 'ReceptorPaisNombre') IS NOT NULL
    BEGIN
        ALTER TABLE DBO.Dte_Documentos DROP COLUMN ReceptorPaisNombre;
    END;

    IF COL_LENGTH('DBO.Dte_Documentos', 'ReceptorPaisCodigo') IS NOT NULL
    BEGIN
        ALTER TABLE DBO.Dte_Documentos DROP COLUMN ReceptorPaisCodigo;
    END;
    """);
        }
    }
}
