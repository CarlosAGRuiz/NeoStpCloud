using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint35_MunicipiosES : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Core_Catalogos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EmpresaId", "EsSistema", "Nombre", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 19, true, "MUNICIPIO_ES", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "44 municipios post-reforma 2024 (Decreto 290). Cada item incluye departamento padre en metadata", null, true, "Municipio / Zona (El Salvador)", null, null });

            migrationBuilder.InsertData(
                table: "Core_CatalogoItems",
                columns: new[] { "Id", "Activo", "CatalogoId", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EsSistema", "MetadataJson", "Orden", "UpdatedAt", "UpdatedBy", "Valor" },
                values: new object[,]
                {
                    { 118, true, 19, "AHUACHAPAN_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"AHUACHAPAN\",\"zona\":\"NORTE\"}", 1, null, null, "Ahuachapán Norte" },
                    { 119, true, 19, "AHUACHAPAN_CENTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"AHUACHAPAN\",\"zona\":\"CENTRO\"}", 2, null, null, "Ahuachapán Centro" },
                    { 120, true, 19, "AHUACHAPAN_SUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"AHUACHAPAN\",\"zona\":\"SUR\"}", 3, null, null, "Ahuachapán Sur" },
                    { 121, true, 19, "SANTA_ANA_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SANTA_ANA\",\"zona\":\"NORTE\"}", 1, null, null, "Santa Ana Norte" },
                    { 122, true, 19, "SANTA_ANA_CENTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SANTA_ANA\",\"zona\":\"CENTRO\"}", 2, null, null, "Santa Ana Centro" },
                    { 123, true, 19, "SANTA_ANA_ESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SANTA_ANA\",\"zona\":\"ESTE\"}", 3, null, null, "Santa Ana Este" },
                    { 124, true, 19, "SONSONATE_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SONSONATE\",\"zona\":\"NORTE\"}", 1, null, null, "Sonsonate Norte" },
                    { 125, true, 19, "SONSONATE_CENTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SONSONATE\",\"zona\":\"CENTRO\"}", 2, null, null, "Sonsonate Centro" },
                    { 126, true, 19, "SONSONATE_ESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SONSONATE\",\"zona\":\"ESTE\"}", 3, null, null, "Sonsonate Este" },
                    { 127, true, 19, "CHALATENANGO_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"CHALATENANGO\",\"zona\":\"NORTE\"}", 1, null, null, "Chalatenango Norte" },
                    { 128, true, 19, "CHALATENANGO_CENTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"CHALATENANGO\",\"zona\":\"CENTRO\"}", 2, null, null, "Chalatenango Centro" },
                    { 129, true, 19, "CHALATENANGO_SUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"CHALATENANGO\",\"zona\":\"SUR\"}", 3, null, null, "Chalatenango Sur" },
                    { 130, true, 19, "LA_LIBERTAD_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_LIBERTAD\",\"zona\":\"NORTE\"}", 1, null, null, "La Libertad Norte" },
                    { 131, true, 19, "LA_LIBERTAD_CENTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_LIBERTAD\",\"zona\":\"CENTRO\"}", 2, null, null, "La Libertad Centro" },
                    { 132, true, 19, "LA_LIBERTAD_SUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_LIBERTAD\",\"zona\":\"SUR\"}", 3, null, null, "La Libertad Sur" },
                    { 133, true, 19, "LA_LIBERTAD_ESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_LIBERTAD\",\"zona\":\"ESTE\"}", 4, null, null, "La Libertad Este" },
                    { 134, true, 19, "LA_LIBERTAD_OESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_LIBERTAD\",\"zona\":\"OESTE\"}", 5, null, null, "La Libertad Oeste" },
                    { 135, true, 19, "LA_LIBERTAD_COSTA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_LIBERTAD\",\"zona\":\"COSTA\"}", 6, null, null, "La Libertad Costa" },
                    { 136, true, 19, "SAN_SALVADOR_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_SALVADOR\",\"zona\":\"NORTE\"}", 1, null, null, "San Salvador Norte" },
                    { 137, true, 19, "SAN_SALVADOR_CENTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_SALVADOR\",\"zona\":\"CENTRO\"}", 2, null, null, "San Salvador Centro" },
                    { 138, true, 19, "SAN_SALVADOR_ESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_SALVADOR\",\"zona\":\"ESTE\"}", 3, null, null, "San Salvador Este" },
                    { 139, true, 19, "SAN_SALVADOR_OESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_SALVADOR\",\"zona\":\"OESTE\"}", 4, null, null, "San Salvador Oeste" },
                    { 140, true, 19, "SAN_SALVADOR_SUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_SALVADOR\",\"zona\":\"SUR\"}", 5, null, null, "San Salvador Sur" },
                    { 141, true, 19, "CUSCATLAN_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"CUSCATLAN\",\"zona\":\"NORTE\"}", 1, null, null, "Cuscatlán Norte" },
                    { 142, true, 19, "CUSCATLAN_SUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"CUSCATLAN\",\"zona\":\"SUR\"}", 2, null, null, "Cuscatlán Sur" },
                    { 143, true, 19, "LA_PAZ_CENTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_PAZ\",\"zona\":\"CENTRO\"}", 1, null, null, "La Paz Centro" },
                    { 144, true, 19, "LA_PAZ_ESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_PAZ\",\"zona\":\"ESTE\"}", 2, null, null, "La Paz Este" },
                    { 145, true, 19, "LA_PAZ_OESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_PAZ\",\"zona\":\"OESTE\"}", 3, null, null, "La Paz Oeste" },
                    { 146, true, 19, "CABANAS_ESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"CABANAS\",\"zona\":\"ESTE\"}", 1, null, null, "Cabañas Este" },
                    { 147, true, 19, "CABANAS_OESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"CABANAS\",\"zona\":\"OESTE\"}", 2, null, null, "Cabañas Oeste" },
                    { 148, true, 19, "SAN_VICENTE_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_VICENTE\",\"zona\":\"NORTE\"}", 1, null, null, "San Vicente Norte" },
                    { 149, true, 19, "SAN_VICENTE_SUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_VICENTE\",\"zona\":\"SUR\"}", 2, null, null, "San Vicente Sur" },
                    { 150, true, 19, "USULUTAN_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"USULUTAN\",\"zona\":\"NORTE\"}", 1, null, null, "Usulután Norte" },
                    { 151, true, 19, "USULUTAN_ESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"USULUTAN\",\"zona\":\"ESTE\"}", 2, null, null, "Usulután Este" },
                    { 152, true, 19, "USULUTAN_OESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"USULUTAN\",\"zona\":\"OESTE\"}", 3, null, null, "Usulután Oeste" },
                    { 153, true, 19, "SAN_MIGUEL_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_MIGUEL\",\"zona\":\"NORTE\"}", 1, null, null, "San Miguel Norte" },
                    { 154, true, 19, "SAN_MIGUEL_CENTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_MIGUEL\",\"zona\":\"CENTRO\"}", 2, null, null, "San Miguel Centro" },
                    { 155, true, 19, "SAN_MIGUEL_OESTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"SAN_MIGUEL\",\"zona\":\"OESTE\"}", 3, null, null, "San Miguel Oeste" },
                    { 156, true, 19, "MORAZAN_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"MORAZAN\",\"zona\":\"NORTE\"}", 1, null, null, "Morazán Norte" },
                    { 157, true, 19, "MORAZAN_SUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"MORAZAN\",\"zona\":\"SUR\"}", 2, null, null, "Morazán Sur" },
                    { 158, true, 19, "LA_UNION_NORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_UNION\",\"zona\":\"NORTE\"}", 1, null, null, "La Unión Norte" },
                    { 159, true, 19, "LA_UNION_SUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"departamento\":\"LA_UNION\",\"zona\":\"SUR\"}", 2, null, null, "La Unión Sur" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 120);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 121);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 122);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 123);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 124);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 125);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 126);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 127);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 128);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 140);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 142);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 143);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 144);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 145);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 146);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 147);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 148);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 149);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 150);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 151);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 152);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 153);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 154);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 155);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 156);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 157);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 158);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 159);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 19);
        }
    }
}
