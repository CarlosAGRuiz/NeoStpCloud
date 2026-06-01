using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint13_CatalogosExtendido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sprint 13.1 — Extender Core_Catalogos y Core_CatalogoItems para soportar
            // versionado de catálogos MH y jerarquía (cascadas territoriales) de ítems.
            //
            // - Catalogo.Version (int, default 1)
            // - Catalogo.MetadataJson (nvarchar(max), nullable)
            // - CatalogoItem.ParentCodigo (nvarchar(50), nullable)
            //
            // El índice único antiguo (Codigo, EmpresaId) se reemplaza por
            // (Codigo, EmpresaId, Version) para permitir múltiples versiones
            // del mismo catálogo coexistiendo.
            //
            // Las filas existentes (HasData en SeedData) quedan automáticamente con
            // Version=1, MetadataJson=null y ParentCodigo=null gracias al
            // defaultValue / nullabilidad — no se requiere backfill manual.

            migrationBuilder.DropIndex(
                name: "IX_Core_Catalogos_Codigo_EmpresaId",
                table: "Core_Catalogos");

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Core_Catalogos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Core_Catalogos",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ParentCodigo",
                table: "Core_CatalogoItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_Catalogos_Codigo_EmpresaId_Activo",
                table: "Core_Catalogos",
                columns: new[] { "Codigo", "EmpresaId", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_Catalogos_Codigo_EmpresaId_Version",
                table: "Core_Catalogos",
                columns: new[] { "Codigo", "EmpresaId", "Version" },
                unique: true,
                filter: "[EmpresaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Core_CatalogoItems_CatalogoId_ParentCodigo",
                table: "Core_CatalogoItems",
                columns: new[] { "CatalogoId", "ParentCodigo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Core_Catalogos_Codigo_EmpresaId_Activo",
                table: "Core_Catalogos");

            migrationBuilder.DropIndex(
                name: "IX_Core_Catalogos_Codigo_EmpresaId_Version",
                table: "Core_Catalogos");

            migrationBuilder.DropIndex(
                name: "IX_Core_CatalogoItems_CatalogoId_ParentCodigo",
                table: "Core_CatalogoItems");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Core_Catalogos");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Core_Catalogos");

            migrationBuilder.DropColumn(
                name: "ParentCodigo",
                table: "Core_CatalogoItems");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Catalogos_Codigo_EmpresaId",
                table: "Core_Catalogos",
                columns: new[] { "Codigo", "EmpresaId" },
                unique: true,
                filter: "[EmpresaId] IS NOT NULL");
        }
    }
}
