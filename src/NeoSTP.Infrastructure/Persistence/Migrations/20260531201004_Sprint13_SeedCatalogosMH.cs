using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint13_SeedCatalogosMH : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Core_Catalogos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EmpresaId", "EsSistema", "MetadataJson", "Nombre", "UpdatedAt", "UpdatedBy", "Version" },
                values: new object[,]
                {
                    { 20, true, "TIPO_CONTINGENCIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-005 Hacienda. 5 motivos oficiales.", null, true, null, "Tipo de Contingencia", null, null, 1 },
                    { 21, true, "TRIBUTO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-015 Hacienda. Subset operativo IVA + tributos específicos. Listado completo importable.", null, true, null, "Tributos", null, null, 1 },
                    { 22, true, "PAIS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-020 Hacienda. Subset LATAM + países frecuentes. Listado completo importable.", null, true, null, "País", null, null, 1 },
                    { 23, true, "MOTIVO_INVALIDACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-024 Hacienda. 3 motivos oficiales.", null, true, null, "Motivo de Invalidación", null, null, 1 },
                    { 24, true, "ACTIVIDAD_ECONOMICA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-019 Hacienda. Subset frecuente. Listado completo importable.", null, true, null, "Actividad Económica", null, null, 1 },
                    { 25, true, "DISTRITO_ES", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-008 Hacienda. Cascada Municipio → Distrito. Populado vía importación.", null, true, null, "Distrito (El Salvador)", null, null, 1 }
                });

            migrationBuilder.InsertData(
                table: "Core_CatalogoItems",
                columns: new[] { "Id", "Activo", "CatalogoId", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EsSistema", "MetadataJson", "Orden", "ParentCodigo", "UpdatedAt", "UpdatedBy", "Valor" },
                values: new object[,]
                {
                    { 160, true, 20, "NO_DISPONIBILIDAD_MH", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"1\"}", 1, null, null, null, "No disponibilidad del sistema de Hacienda" },
                    { 161, true, 20, "CONEXION_MH", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"2\"}", 2, null, null, null, "Falla en la conexión del emisor con Hacienda" },
                    { 162, true, 20, "SERVICIOS_SVT", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"3\"}", 3, null, null, null, "Falla en los servicios de SVT del emisor" },
                    { 163, true, 20, "CONEXION_RECEPTOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"4\"}", 4, null, null, null, "Falla de conexión con el receptor" },
                    { 164, true, 20, "OTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"5\"}", 5, null, null, null, "Otros (justificar en detalle)" },
                    { 165, true, 21, "IVA_13", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"20\",\"tasa\":0.13}", 1, null, null, null, "IVA - Impuesto al Valor Agregado 13%" },
                    { 166, true, 21, "IVA_EXPORT_0", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"C8\",\"tasa\":0}", 2, null, null, null, "IVA exportaciones 0%" },
                    { 167, true, 21, "IVA_RETENIDO_1", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"22\",\"tasa\":0.01}", 3, null, null, null, "IVA Retención 1%" },
                    { 168, true, 21, "IVA_RETENIDO_13", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"C4\",\"tasa\":0.13}", 4, null, null, null, "IVA Retención 13%" },
                    { 169, true, 21, "RENTA_10", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"C9\",\"tasa\":0.10}", 5, null, null, null, "Renta Retención 10%" },
                    { 170, true, 21, "FOVIAL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"D1\"}", 6, null, null, null, "FOVIAL — Fondo de Conservación Vial" },
                    { 171, true, 21, "COTRANS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"C5\"}", 7, null, null, null, "COTRANS — Contribución especial al transporte" },
                    { 172, true, 21, "TURISMO_5", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"59\",\"tasa\":0.05}", 8, null, null, null, "Turismo: alojamiento 5%" },
                    { 173, true, 21, "TURISMO_SALIDA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"71\"}", 9, null, null, null, "Turismo: salida del país" },
                    { 174, true, 21, "ESPECIAL_SEGURIDAD", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"D5\"}", 10, null, null, null, "Contribución especial seguridad pública" },
                    { 175, true, 21, "ALCABALA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"D4\"}", 11, null, null, null, "Impuesto Especial al primer matriculación" },
                    { 176, true, 21, "FONAES", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"32\"}", 12, null, null, null, "FONAES — Fondo Energético" },
                    { 177, true, 22, "SV", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9300\"}", 1, null, null, null, "El Salvador" },
                    { 178, true, 22, "GT", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9305\"}", 2, null, null, null, "Guatemala" },
                    { 179, true, 22, "HN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9310\"}", 3, null, null, null, "Honduras" },
                    { 180, true, 22, "NI", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9315\"}", 4, null, null, null, "Nicaragua" },
                    { 181, true, 22, "CR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9320\"}", 5, null, null, null, "Costa Rica" },
                    { 182, true, 22, "PA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9325\"}", 6, null, null, null, "Panamá" },
                    { 183, true, 22, "BZ", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9302\"}", 7, null, null, null, "Belice" },
                    { 184, true, 22, "MX", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9301\"}", 8, null, null, null, "México" },
                    { 185, true, 22, "US", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9905\"}", 9, null, null, null, "Estados Unidos" },
                    { 186, true, 22, "CA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9910\"}", 10, null, null, null, "Canadá" },
                    { 187, true, 22, "CO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9405\"}", 11, null, null, null, "Colombia" },
                    { 188, true, 22, "VE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9410\"}", 12, null, null, null, "Venezuela" },
                    { 189, true, 22, "EC", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9415\"}", 13, null, null, null, "Ecuador" },
                    { 190, true, 22, "PE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9420\"}", 14, null, null, null, "Perú" },
                    { 191, true, 22, "BO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9425\"}", 15, null, null, null, "Bolivia" },
                    { 192, true, 22, "CL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9430\"}", 16, null, null, null, "Chile" },
                    { 193, true, 22, "AR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9435\"}", 17, null, null, null, "Argentina" },
                    { 194, true, 22, "BR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9440\"}", 18, null, null, null, "Brasil" },
                    { 195, true, 22, "UY", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9445\"}", 19, null, null, null, "Uruguay" },
                    { 196, true, 22, "PY", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9450\"}", 20, null, null, null, "Paraguay" },
                    { 197, true, 22, "DO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9605\"}", 21, null, null, null, "República Dominicana" },
                    { 198, true, 22, "CU", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9610\"}", 22, null, null, null, "Cuba" },
                    { 199, true, 22, "ES", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9705\"}", 23, null, null, null, "España" },
                    { 200, true, 22, "CN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9805\"}", 24, null, null, null, "China" },
                    { 201, true, 22, "JP", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"9810\"}", 25, null, null, null, "Japón" },
                    { 202, true, 23, "ERROR_INFO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"1\"}", 1, null, null, null, "Error en la información del documento tributario" },
                    { 203, true, 23, "RESCINDIR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"2\"}", 2, null, null, null, "Rescindir de la operación realizada" },
                    { 204, true, 23, "OTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"3\"}", 3, null, null, null, "Otros (justificar en detalle)" },
                    { 205, true, 24, "AGRICULTURA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"01111\"}", 1, null, null, null, "Agricultura, ganadería y pesca" },
                    { 206, true, 24, "MINERIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"05101\"}", 2, null, null, null, "Explotación de minas y canteras" },
                    { 207, true, 24, "INDUSTRIA_ALIM", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"10110\"}", 3, null, null, null, "Industrias manufactureras — alimentos" },
                    { 208, true, 24, "CONSTRUCCION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"41001\"}", 4, null, null, null, "Construcción" },
                    { 209, true, 24, "COMERCIO_MAYOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"46900\"}", 5, null, null, null, "Comercio al por mayor" },
                    { 210, true, 24, "COMERCIO_MENOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"47190\"}", 6, null, null, null, "Comercio al por menor" },
                    { 211, true, 24, "TRANSPORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"49230\"}", 7, null, null, null, "Transporte y almacenamiento" },
                    { 212, true, 24, "ALOJAMIENTO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"55101\"}", 8, null, null, null, "Alojamiento y servicios de comida" },
                    { 213, true, 24, "INFORMATICA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"62010\"}", 9, null, null, null, "Servicios de información y comunicación" },
                    { 214, true, 24, "FINANCIEROS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"64190\"}", 10, null, null, null, "Servicios financieros y seguros" },
                    { 215, true, 24, "INMOBILIARIO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"68101\"}", 11, null, null, null, "Actividades inmobiliarias" },
                    { 216, true, 24, "PROFESIONALES", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"69100\"}", 12, null, null, null, "Servicios profesionales y técnicos" },
                    { 217, true, 24, "ADMIN_PUBLICA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"84110\"}", 13, null, null, null, "Administración pública" },
                    { 218, true, 24, "EDUCACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"85100\"}", 14, null, null, null, "Enseñanza" },
                    { 219, true, 24, "SALUD", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"86101\"}", 15, null, null, null, "Servicios de salud y asistencia social" },
                    { 220, true, 24, "ARTES", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"90001\"}", 16, null, null, null, "Artes, entretenimiento y recreación" },
                    { 221, true, 24, "OTROS_SERV", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"94110\"}", 17, null, null, null, "Otras actividades de servicios" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 160);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 161);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 162);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 163);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 164);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 165);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 166);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 167);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 168);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 169);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 170);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 171);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 172);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 173);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 174);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 175);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 176);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 177);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 178);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 179);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 180);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 181);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 182);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 183);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 184);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 185);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 186);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 187);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 188);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 189);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 190);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 191);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 192);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 193);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 194);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 195);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 196);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 197);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 198);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 199);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 200);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 201);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 202);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 203);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 204);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 205);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 206);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 207);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 208);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 209);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 210);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 211);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 212);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 213);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 214);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 215);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 216);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 217);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 218);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 219);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 220);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 221);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 24);
        }
    }
}
