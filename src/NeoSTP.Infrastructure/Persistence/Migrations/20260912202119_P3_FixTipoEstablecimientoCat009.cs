using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3_FixTipoEstablecimientoCat009 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 46,
                column: "MetadataJson",
                value: "{\"codigoMH\":\"02\"}");

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 47,
                column: "MetadataJson",
                value: "{\"codigoMH\":\"01\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 46,
                column: "MetadataJson",
                value: "{\"codigoMH\":\"01\"}");

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 47,
                column: "MetadataJson",
                value: "{\"codigoMH\":\"02\"}");
        }
    }
}
