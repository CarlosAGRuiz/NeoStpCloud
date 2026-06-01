using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint13_CatalogosMhOficial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 96);

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

            migrationBuilder.InsertData(
                table: "Core_CatalogoItems",
                columns: new[] { "Id", "Activo", "CatalogoId", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EsSistema", "MetadataJson", "Orden", "ParentCodigo", "UpdatedAt", "UpdatedBy", "Valor" },
                values: new object[,]
                {
                    { 1003, true, 15, "01", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"01\"}", 1, null, null, null, "Metro" },
                    { 1004, true, 15, "02", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"02\"}", 2, null, null, null, "Yarda" },
                    { 1005, true, 15, "03", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"03\"}", 3, null, null, null, "Vara" },
                    { 1006, true, 15, "04", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"04\"}", 4, null, null, null, "Pie" },
                    { 1007, true, 15, "05", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"05\"}", 5, null, null, null, "Pulgada" },
                    { 1008, true, 15, "06", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"06\"}", 6, null, null, null, "Milímetro" },
                    { 1009, true, 15, "08", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"08\"}", 7, null, null, null, "Milla cuadrada" },
                    { 1010, true, 15, "09", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"09\"}", 8, null, null, null, "Kilómetro cuadrado" },
                    { 1011, true, 15, "10", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"10\"}", 9, null, null, null, "Hectárea" },
                    { 1012, true, 15, "11", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"11\"}", 10, null, null, null, "Manzana" },
                    { 1013, true, 15, "12", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"12\"}", 11, null, null, null, "Acre" },
                    { 1014, true, 15, "13", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"13\"}", 12, null, null, null, "Metro cuadrado" },
                    { 1015, true, 15, "14", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"14\"}", 13, null, null, null, "Yarda cuadrada" },
                    { 1016, true, 15, "15", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"15\"}", 14, null, null, null, "Vara cuadrada" },
                    { 1017, true, 15, "16", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"16\"}", 15, null, null, null, "Pie cuadrado" },
                    { 1018, true, 15, "17", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"17\"}", 16, null, null, null, "Pulgada cuadrada" },
                    { 1019, true, 15, "18", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"18\"}", 17, null, null, null, "Metro cúbico" },
                    { 1020, true, 15, "19", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"19\"}", 18, null, null, null, "Yarda cúbica" },
                    { 1021, true, 15, "20", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"20\"}", 19, null, null, null, "Barril" },
                    { 1022, true, 15, "21", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"21\"}", 20, null, null, null, "Pie cúbico" },
                    { 1023, true, 15, "22", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"22\"}", 21, null, null, null, "Galón" },
                    { 1024, true, 15, "23", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"23\"}", 22, null, null, null, "Litro" },
                    { 1025, true, 15, "24", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"24\"}", 23, null, null, null, "Botella" },
                    { 1026, true, 15, "25", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"25\"}", 24, null, null, null, "Pulgada cúbica" },
                    { 1027, true, 15, "26", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"26\"}", 25, null, null, null, "Mililitro" },
                    { 1028, true, 15, "27", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"27\"}", 26, null, null, null, "Onza fluida" },
                    { 1029, true, 15, "29", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"29\"}", 27, null, null, null, "Tonelada métrica" },
                    { 1030, true, 15, "30", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"30\"}", 28, null, null, null, "Tonelada" },
                    { 1031, true, 15, "31", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"31\"}", 29, null, null, null, "Quintal métrico" },
                    { 1032, true, 15, "32", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"32\"}", 30, null, null, null, "Quintal" },
                    { 1033, true, 15, "33", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"33\"}", 31, null, null, null, "Arroba" },
                    { 1034, true, 15, "34", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"34\"}", 32, null, null, null, "Kilogramo" },
                    { 1035, true, 15, "35", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"35\"}", 33, null, null, null, "Libra troy" },
                    { 1036, true, 15, "36", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"36\"}", 34, null, null, null, "Libra" },
                    { 1037, true, 15, "37", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"37\"}", 35, null, null, null, "Onza troy" },
                    { 1038, true, 15, "38", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"38\"}", 36, null, null, null, "Onza" },
                    { 1039, true, 15, "39", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"39\"}", 37, null, null, null, "Gramo" },
                    { 1040, true, 15, "40", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"40\"}", 38, null, null, null, "Miligramo" },
                    { 1041, true, 15, "42", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"42\"}", 39, null, null, null, "Megawatt" },
                    { 1042, true, 15, "43", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"43\"}", 40, null, null, null, "Kilowatt" },
                    { 1043, true, 15, "44", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"44\"}", 41, null, null, null, "Watt" },
                    { 1044, true, 15, "45", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"45\"}", 42, null, null, null, "Megavoltio-amperio" },
                    { 1045, true, 15, "46", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"46\"}", 43, null, null, null, "Kilovoltio-amperio" },
                    { 1046, true, 15, "47", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"47\"}", 44, null, null, null, "Voltio-amperio" },
                    { 1047, true, 15, "49", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"49\"}", 45, null, null, null, "Gigawatt-hora" },
                    { 1048, true, 15, "50", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"50\"}", 46, null, null, null, "Megawatt-hora" },
                    { 1049, true, 15, "51", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"51\"}", 47, null, null, null, "Kilowatt-hora" },
                    { 1050, true, 15, "52", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"52\"}", 48, null, null, null, "Watt-hora" },
                    { 1051, true, 15, "53", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"53\"}", 49, null, null, null, "Kilovoltio" },
                    { 1052, true, 15, "54", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"54\"}", 50, null, null, null, "Voltio" },
                    { 1053, true, 15, "55", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"55\"}", 51, null, null, null, "Millar" },
                    { 1054, true, 15, "56", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"56\"}", 52, null, null, null, "Medio millar" },
                    { 1055, true, 15, "57", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"57\"}", 53, null, null, null, "Ciento" },
                    { 1056, true, 15, "58", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"58\"}", 54, null, null, null, "Docena" },
                    { 1057, true, 15, "59", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"59\"}", 55, null, null, null, "Unidad" },
                    { 1058, true, 15, "99", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"99\"}", 56, null, null, null, "Otra" },
                    { 1062, true, 22, "9300", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9300\", \"nombreMH\": \"EL SALVADOR\"}", 1, null, null, null, "El Salvador" },
                    { 1063, true, 22, "9303", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9303\", \"nombreMH\": \"AFGANISTÁN\"}", 2, null, null, null, "Afganistán" },
                    { 1064, true, 22, "9304", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9304\", \"nombreMH\": \"ALAND\"}", 3, null, null, null, "Aland" },
                    { 1065, true, 22, "9306", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9306\", \"nombreMH\": \"ALBANIA\"}", 4, null, null, null, "Albania" },
                    { 1066, true, 22, "9309", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9309\", \"nombreMH\": \"ALEMANIA OCCID\"}", 5, null, null, null, "Alemania Occid" },
                    { 1067, true, 22, "9310", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9310\", \"nombreMH\": \"ALEMANIA ORIENT\"}", 6, null, null, null, "Alemania Orient" },
                    { 1068, true, 22, "9311", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9311\", \"nombreMH\": \"ALEMANIA\"}", 7, null, null, null, "Alemania" },
                    { 1069, true, 22, "9315", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9315\", \"nombreMH\": \"ALTO VOLTA\"}", 8, null, null, null, "Alto Volta" },
                    { 1070, true, 22, "9317", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9317\", \"nombreMH\": \"ANDORRA\"}", 9, null, null, null, "Andorra" },
                    { 1071, true, 22, "9318", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9318\", \"nombreMH\": \"ANGOLA\"}", 10, null, null, null, "Angola" },
                    { 1072, true, 22, "9319", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9319\", \"nombreMH\": \"ANTIG Y BARBUDA\"}", 11, null, null, null, "Antig Y Barbuda" },
                    { 1073, true, 22, "9320", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9320\", \"nombreMH\": \"ANGUILA\"}", 12, null, null, null, "Anguila" },
                    { 1074, true, 22, "9324", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9324\", \"nombreMH\": \"ARABIA SAUDITA\"}", 13, null, null, null, "Arabia Saudita" },
                    { 1075, true, 22, "9327", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9327\", \"nombreMH\": \"ARGELIA\"}", 14, null, null, null, "Argelia" },
                    { 1076, true, 22, "9330", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9330\", \"nombreMH\": \"ARGENTINA\"}", 15, null, null, null, "Argentina" },
                    { 1077, true, 22, "9332", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9332\", \"nombreMH\": \"ARUBA\"}", 16, null, null, null, "Aruba" },
                    { 1078, true, 22, "9333", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9333\", \"nombreMH\": \"AUSTRALIA\"}", 17, null, null, null, "Australia" },
                    { 1079, true, 22, "9336", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9336\", \"nombreMH\": \"AUSTRIA\"}", 18, null, null, null, "Austria" },
                    { 1080, true, 22, "9338", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9338\", \"nombreMH\": \"AZERBAIYÁN\"}", 19, null, null, null, "Azerbaiyán" },
                    { 1081, true, 22, "9339", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9339\", \"nombreMH\": \"BANGLADESH\"}", 20, null, null, null, "Bangladesh" },
                    { 1082, true, 22, "9342", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9342\", \"nombreMH\": \"BAHRÉIN\"}", 21, null, null, null, "Bahréin" },
                    { 1083, true, 22, "9345", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9345\", \"nombreMH\": \"BARBADOS\"}", 22, null, null, null, "Barbados" },
                    { 1084, true, 22, "9348", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9348\", \"nombreMH\": \"BÉLGICA\"}", 23, null, null, null, "Bélgica" },
                    { 1085, true, 22, "9349", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9349\", \"nombreMH\": \"BELICE\"}", 24, null, null, null, "Belice" },
                    { 1086, true, 22, "9350", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9350\", \"nombreMH\": \"BENÍN\"}", 25, null, null, null, "Benín" },
                    { 1087, true, 22, "9353", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9353\", \"nombreMH\": \"BIELORRUSIA\"}", 26, null, null, null, "Bielorrusia" },
                    { 1088, true, 22, "9354", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9354\", \"nombreMH\": \"BIRMANIA\"}", 27, null, null, null, "Birmania" },
                    { 1089, true, 22, "9357", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9357\", \"nombreMH\": \"BOLIVIA\"}", 28, null, null, null, "Bolivia" },
                    { 1090, true, 22, "9359", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9359\", \"nombreMH\": \"BOSNIA Y HERZEGOVINA\"}", 29, null, null, null, "Bosnia Y Herzegovina" },
                    { 1091, true, 22, "9360", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9360\", \"nombreMH\": \"BOTSWANA\"}", 30, null, null, null, "Botswana" },
                    { 1092, true, 22, "9363", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9363\", \"nombreMH\": \"BRASIL\"}", 31, null, null, null, "Brasil" },
                    { 1093, true, 22, "9366", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9366\", \"nombreMH\": \"BRUNÉI\"}", 32, null, null, null, "Brunéi" },
                    { 1094, true, 22, "9369", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9369\", \"nombreMH\": \"BULGARIA\"}", 33, null, null, null, "Bulgaria" },
                    { 1095, true, 22, "9371", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9371\", \"nombreMH\": \"BURKINA FASO\"}", 34, null, null, null, "Burkina Faso" },
                    { 1096, true, 22, "9372", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9372\", \"nombreMH\": \"BURUNDI\"}", 35, null, null, null, "Burundi" },
                    { 1097, true, 22, "9374", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9374\", \"nombreMH\": \"BOPHUTHATSWANA\"}", 36, null, null, null, "Bophuthatswana" },
                    { 1098, true, 22, "9375", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9375\", \"nombreMH\": \"BUTÁN\"}", 37, null, null, null, "Bután" },
                    { 1099, true, 22, "9376", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9376\", \"nombreMH\": \"CABINDA\"}", 38, null, null, null, "Cabinda" },
                    { 1100, true, 22, "9377", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9377\", \"nombreMH\": \"CABO VERDE\"}", 39, null, null, null, "Cabo Verde" },
                    { 1101, true, 22, "9378", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9378\", \"nombreMH\": \"CAMBOYA\"}", 40, null, null, null, "Camboya" },
                    { 1102, true, 22, "9381", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9381\", \"nombreMH\": \"CAMERÚN\"}", 41, null, null, null, "Camerún" },
                    { 1103, true, 22, "9384", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9384\", \"nombreMH\": \"CANADÁ\"}", 42, null, null, null, "Canadá" },
                    { 1104, true, 22, "9387", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9387\", \"nombreMH\": \"CEILÁN\"}", 43, null, null, null, "Ceilán" },
                    { 1105, true, 22, "9390", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9390\", \"nombreMH\": \"CTRO AFRIC REP\"}", 44, null, null, null, "Ctro Afric Rep" },
                    { 1106, true, 22, "9393", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9393\", \"nombreMH\": \"COLOMBIA\"}", 45, null, null, null, "Colombia" },
                    { 1107, true, 22, "9394", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9394\", \"nombreMH\": \"COMORAS-ISLAS\"}", 46, null, null, null, "Comoras-Islas" },
                    { 1108, true, 22, "9396", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9396\", \"nombreMH\": \"CONGO REP DEL\"}", 47, null, null, null, "Congo Rep Del" },
                    { 1109, true, 22, "9399", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9399\", \"nombreMH\": \"CONGO REP DEMOC\"}", 48, null, null, null, "Congo Rep Democ" },
                    { 1110, true, 22, "9402", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9402\", \"nombreMH\": \"COREA NORTE\"}", 49, null, null, null, "Corea Norte" },
                    { 1111, true, 22, "9405", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9405\", \"nombreMH\": \"COREA SUR\"}", 50, null, null, null, "Corea Sur" },
                    { 1112, true, 22, "9408", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9408\", \"nombreMH\": \"COSTA DE MARFIL\"}", 51, null, null, null, "Costa De Marfil" },
                    { 1113, true, 22, "9411", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9411\", \"nombreMH\": \"COSTA RICA\"}", 52, null, null, null, "Costa Rica" },
                    { 1114, true, 22, "9414", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9414\", \"nombreMH\": \"CUBA\"}", 53, null, null, null, "Cuba" },
                    { 1115, true, 22, "9415", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9415\", \"nombreMH\": \"CURAZAO\"}", 54, null, null, null, "Curazao" },
                    { 1116, true, 22, "9417", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9417\", \"nombreMH\": \"CHAD\"}", 55, null, null, null, "Chad" },
                    { 1117, true, 22, "9420", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9420\", \"nombreMH\": \"CHECOSLOVAQUIA\"}", 56, null, null, null, "Checoslovaquia" },
                    { 1118, true, 22, "9423", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9423\", \"nombreMH\": \"CHILE\"}", 57, null, null, null, "Chile" },
                    { 1119, true, 22, "9426", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9426\", \"nombreMH\": \"CHINA REP POPUL\"}", 58, null, null, null, "China Rep Popul" },
                    { 1120, true, 22, "9432", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9432\", \"nombreMH\": \"CHIPRE\"}", 59, null, null, null, "Chipre" },
                    { 1121, true, 22, "9435", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9435\", \"nombreMH\": \"DAHOMEY\"}", 60, null, null, null, "Dahomey" },
                    { 1122, true, 22, "9438", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9438\", \"nombreMH\": \"DINAMARCA\"}", 61, null, null, null, "Dinamarca" },
                    { 1123, true, 22, "9439", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9439\", \"nombreMH\": \"DJIBOUTI\"}", 62, null, null, null, "Djibouti" },
                    { 1124, true, 22, "9440", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9440\", \"nombreMH\": \"DOMINICA\"}", 63, null, null, null, "Dominica" },
                    { 1125, true, 22, "9441", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9441\", \"nombreMH\": \"DOMINICANA REP\"}", 64, null, null, null, "Dominicana Rep" },
                    { 1126, true, 22, "9444", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9444\", \"nombreMH\": \"ECUADOR\"}", 65, null, null, null, "Ecuador" },
                    { 1127, true, 22, "9446", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9446\", \"nombreMH\": \"EMIRAT ARAB UNI\"}", 66, null, null, null, "Emirat Arab Uni" },
                    { 1128, true, 22, "9447", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9447\", \"nombreMH\": \"ESPAÑA\"}", 67, null, null, null, "España" },
                    { 1129, true, 22, "9449", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9449\", \"nombreMH\": \"ESLOVAQUIA\"}", 68, null, null, null, "Eslovaquia" },
                    { 1130, true, 22, "9450", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9450\", \"nombreMH\": \"EE UU\"}", 69, null, null, null, "Ee Uu" },
                    { 1131, true, 22, "9451", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9451\", \"nombreMH\": \"ESLOVENIA\"}", 70, null, null, null, "Eslovenia" },
                    { 1132, true, 22, "9452", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9452\", \"nombreMH\": \"WALLIS Y FUTUNA\"}", 71, null, null, null, "Wallis Y Futuna" },
                    { 1133, true, 22, "9453", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9453\", \"nombreMH\": \"ETIOPIA\"}", 72, null, null, null, "Etiopia" },
                    { 1134, true, 22, "9454", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9454\", \"nombreMH\": \"ERITREA\"}", 73, null, null, null, "Eritrea" },
                    { 1135, true, 22, "9456", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9456\", \"nombreMH\": \"FIJI-ISLAS\"}", 74, null, null, null, "Fiji-Islas" },
                    { 1136, true, 22, "9457", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9457\", \"nombreMH\": \"ESTONIA\"}", 75, null, null, null, "Estonia" },
                    { 1137, true, 22, "9459", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9459\", \"nombreMH\": \"FILIPINAS\"}", 76, null, null, null, "Filipinas" },
                    { 1138, true, 22, "9462", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9462\", \"nombreMH\": \"FINLANDIA\"}", 77, null, null, null, "Finlandia" },
                    { 1139, true, 22, "9465", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9465\", \"nombreMH\": \"FRANCIA\"}", 78, null, null, null, "Francia" },
                    { 1140, true, 22, "9468", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9468\", \"nombreMH\": \"GABÓN\"}", 79, null, null, null, "Gabón" },
                    { 1141, true, 22, "9471", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9471\", \"nombreMH\": \"GAMBIA\"}", 80, null, null, null, "Gambia" },
                    { 1142, true, 22, "9472", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9472\", \"nombreMH\": \"GEORGIA\"}", 81, null, null, null, "Georgia" },
                    { 1143, true, 22, "9474", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9474\", \"nombreMH\": \"GHANA\"}", 82, null, null, null, "Ghana" },
                    { 1144, true, 22, "9477", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9477\", \"nombreMH\": \"GIBRALTAR\"}", 83, null, null, null, "Gibraltar" },
                    { 1145, true, 22, "9480", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9480\", \"nombreMH\": \"GRECIA\"}", 84, null, null, null, "Grecia" },
                    { 1146, true, 22, "9481", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9481\", \"nombreMH\": \"GRENADA\"}", 85, null, null, null, "Grenada" },
                    { 1147, true, 22, "9482", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9482\", \"nombreMH\": \"GROENLANDIA\"}", 86, null, null, null, "Groenlandia" },
                    { 1148, true, 22, "9483", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9483\", \"nombreMH\": \"GUATEMALA\"}", 87, null, null, null, "Guatemala" },
                    { 1149, true, 22, "9486", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9486\", \"nombreMH\": \"GUINEA\"}", 88, null, null, null, "Guinea" },
                    { 1150, true, 22, "9487", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9487\", \"nombreMH\": \"GUYANA\"}", 89, null, null, null, "Guyana" },
                    { 1151, true, 22, "9489", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9489\", \"nombreMH\": \"GUADALUPE\"}", 90, null, null, null, "Guadalupe" },
                    { 1152, true, 22, "9490", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9490\", \"nombreMH\": \"GUAM\"}", 91, null, null, null, "Guam" },
                    { 1153, true, 22, "9491", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9491\", \"nombreMH\": \"GUAYANA FRANCESA\"}", 92, null, null, null, "Guayana Francesa" },
                    { 1154, true, 22, "9492", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9492\", \"nombreMH\": \"GUERNSEY\"}", 93, null, null, null, "Guernsey" },
                    { 1155, true, 22, "9493", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9493\", \"nombreMH\": \"GUINEA ECUATORIAL\"}", 94, null, null, null, "Guinea Ecuatorial" },
                    { 1156, true, 22, "9494", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9494\", \"nombreMH\": \"GUINEA-BISSAU\"}", 95, null, null, null, "Guinea-Bissau" },
                    { 1157, true, 22, "9495", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9495\", \"nombreMH\": \"HAITÍ\"}", 96, null, null, null, "Haití" },
                    { 1158, true, 22, "9498", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9498\", \"nombreMH\": \"HOLANDA\"}", 97, null, null, null, "Holanda" },
                    { 1159, true, 22, "9501", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9501\", \"nombreMH\": \"HONDURAS\"}", 98, null, null, null, "Honduras" },
                    { 1160, true, 22, "9504", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9504\", \"nombreMH\": \"HONG KONG\"}", 99, null, null, null, "Hong Kong" },
                    { 1161, true, 22, "9507", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9507\", \"nombreMH\": \"HUNGRÍA\"}", 100, null, null, null, "Hungría" },
                    { 1162, true, 22, "9510", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9510\", \"nombreMH\": \"INDIA\"}", 101, null, null, null, "India" },
                    { 1163, true, 22, "9513", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9513\", \"nombreMH\": \"INDONESIA\"}", 102, null, null, null, "Indonesia" },
                    { 1164, true, 22, "9514", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9514\", \"nombreMH\": \"INGLATERRA Y GALES\"}", 103, null, null, null, "Inglaterra Y Gales" },
                    { 1165, true, 22, "9516", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9516\", \"nombreMH\": \"IRAK\"}", 104, null, null, null, "Irak" },
                    { 1166, true, 22, "9519", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9519\", \"nombreMH\": \"IRÁN\"}", 105, null, null, null, "Irán" },
                    { 1167, true, 22, "9521", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9521\", \"nombreMH\": \"ISLA DE MAN\"}", 106, null, null, null, "Isla De Man" },
                    { 1168, true, 22, "9522", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9522\", \"nombreMH\": \"IRLANDA\"}", 107, null, null, null, "Irlanda" },
                    { 1169, true, 22, "9523", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9523\", \"nombreMH\": \"ISLA DE NAVIDAD\"}", 108, null, null, null, "Isla De Navidad" },
                    { 1170, true, 22, "9524", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9524\", \"nombreMH\": \"ISLA DE COCOS\"}", 109, null, null, null, "Isla De Cocos" },
                    { 1171, true, 22, "9525", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9525\", \"nombreMH\": \"ISLANDIA\"}", 110, null, null, null, "Islandia" },
                    { 1172, true, 22, "9526", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9526\", \"nombreMH\": \"ISLAS SALOMÓN\"}", 111, null, null, null, "Islas Salomón" },
                    { 1173, true, 22, "9527", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9527\", \"nombreMH\": \"ISLAS COOK\"}", 112, null, null, null, "Islas Cook" },
                    { 1174, true, 22, "9528", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9528\", \"nombreMH\": \"ISRAEL\"}", 113, null, null, null, "Israel" },
                    { 1175, true, 22, "9529", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9529\", \"nombreMH\": \"ISLAS FEROE\"}", 114, null, null, null, "Islas Feroe" },
                    { 1176, true, 22, "9530", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9530\", \"nombreMH\": \"ISLAS AZORES\"}", 115, null, null, null, "Islas Azores" },
                    { 1177, true, 22, "9531", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9531\", \"nombreMH\": \"ITALIA\"}", 116, null, null, null, "Italia" },
                    { 1178, true, 22, "9532", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9532\", \"nombreMH\": \"ISLA QESHM\"}", 117, null, null, null, "Isla Qeshm" },
                    { 1179, true, 22, "9533", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9533\", \"nombreMH\": \"ISLAS MALVINAS\"}", 118, null, null, null, "Islas Malvinas" },
                    { 1180, true, 22, "9534", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9534\", \"nombreMH\": \"JAMAICA\"}", 119, null, null, null, "Jamaica" },
                    { 1181, true, 22, "9535", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9535\", \"nombreMH\": \"ISLAS MARIANAS DEL NORTE\"}", 120, null, null, null, "Islas Marianas Del Norte" },
                    { 1182, true, 22, "9536", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9536\", \"nombreMH\": \"ISLAS MARSHALL\"}", 121, null, null, null, "Islas Marshall" },
                    { 1183, true, 22, "9537", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9537\", \"nombreMH\": \"JAPÓN\"}", 122, null, null, null, "Japón" },
                    { 1184, true, 22, "9538", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9538\", \"nombreMH\": \"ISLAS PITCAIM\"}", 123, null, null, null, "Islas Pitcaim" },
                    { 1185, true, 22, "9539", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9539\", \"nombreMH\": \"ISLAS TURCAS Y CAICOS\"}", 124, null, null, null, "Islas Turcas Y Caicos" },
                    { 1186, true, 22, "9540", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9540\", \"nombreMH\": \"JORDANIA\"}", 125, null, null, null, "Jordania" },
                    { 1187, true, 22, "9541", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9541\", \"nombreMH\": \"KASAKISTAN\"}", 126, null, null, null, "Kasakistan" },
                    { 1188, true, 22, "9542", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9542\", \"nombreMH\": \"ISLAS ULTRAMARINAS DE EE UU\"}", 127, null, null, null, "Islas Ultramarinas De Ee Uu" },
                    { 1189, true, 22, "9543", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9543\", \"nombreMH\": \"KENIA\"}", 128, null, null, null, "Kenia" },
                    { 1190, true, 22, "9544", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9544\", \"nombreMH\": \"KIRIBATI\"}", 129, null, null, null, "Kiribati" },
                    { 1191, true, 22, "9545", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9545\", \"nombreMH\": \"ISLAS VÍRGENES ESTADOUNIDENSES\"}", 130, null, null, null, "Islas Vírgenes Estadounidenses" },
                    { 1192, true, 22, "9546", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9546\", \"nombreMH\": \"KUWAIT\"}", 131, null, null, null, "Kuwait" },
                    { 1193, true, 22, "9547", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9547\", \"nombreMH\": \"JERSEY\"}", 132, null, null, null, "Jersey" },
                    { 1194, true, 22, "9548", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9548\", \"nombreMH\": \"KIRGUISTÁN\"}", 133, null, null, null, "Kirguistán" },
                    { 1195, true, 22, "9549", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9549\", \"nombreMH\": \"LAOS\"}", 134, null, null, null, "Laos" },
                    { 1196, true, 22, "9551", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9551\", \"nombreMH\": \"LETONIA\"}", 135, null, null, null, "Letonia" },
                    { 1197, true, 22, "9552", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9552\", \"nombreMH\": \"LESOTHO\"}", 136, null, null, null, "Lesotho" },
                    { 1198, true, 22, "9555", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9555\", \"nombreMH\": \"LÍBANO\"}", 137, null, null, null, "Líbano" },
                    { 1199, true, 22, "9558", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9558\", \"nombreMH\": \"LIBERIA\"}", 138, null, null, null, "Liberia" },
                    { 1200, true, 22, "9561", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9561\", \"nombreMH\": \"LIBIA\"}", 139, null, null, null, "Libia" },
                    { 1201, true, 22, "9564", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9564\", \"nombreMH\": \"LIECHTENSTEIN\"}", 140, null, null, null, "Liechtenstein" },
                    { 1202, true, 22, "9565", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9565\", \"nombreMH\": \"LITUANIA\"}", 141, null, null, null, "Lituania" },
                    { 1203, true, 22, "9567", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9567\", \"nombreMH\": \"LUXEMBURGO\"}", 142, null, null, null, "Luxemburgo" },
                    { 1204, true, 22, "9568", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9568\", \"nombreMH\": \"MACAO\"}", 143, null, null, null, "Macao" },
                    { 1205, true, 22, "9570", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9570\", \"nombreMH\": \"MADAGASCAR\"}", 144, null, null, null, "Madagascar" },
                    { 1206, true, 22, "9571", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9571\", \"nombreMH\": \"MACEDONIA\"}", 145, null, null, null, "Macedonia" },
                    { 1207, true, 22, "9573", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9573\", \"nombreMH\": \"MALASIA\"}", 146, null, null, null, "Malasia" },
                    { 1208, true, 22, "9574", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9574\", \"nombreMH\": \"MALI\"}", 147, null, null, null, "Mali" },
                    { 1209, true, 22, "9576", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9576\", \"nombreMH\": \"MALAWI\"}", 148, null, null, null, "Malawi" },
                    { 1210, true, 22, "9577", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9577\", \"nombreMH\": \"MALDIVAS\"}", 149, null, null, null, "Maldivas" },
                    { 1211, true, 22, "9579", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9579\", \"nombreMH\": \"MALI\"}", 150, null, null, null, "Mali" },
                    { 1212, true, 22, "9582", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9582\", \"nombreMH\": \"MALTA\"}", 151, null, null, null, "Malta" },
                    { 1213, true, 22, "9585", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9585\", \"nombreMH\": \"MARRUECOS\"}", 152, null, null, null, "Marruecos" },
                    { 1214, true, 22, "9591", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9591\", \"nombreMH\": \"MASCATE Y OMÁN\"}", 153, null, null, null, "Mascate Y Omán" },
                    { 1215, true, 22, "9594", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9594\", \"nombreMH\": \"MAURICIO\"}", 154, null, null, null, "Mauricio" },
                    { 1216, true, 22, "9597", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9597\", \"nombreMH\": \"MAURITANIA\"}", 155, null, null, null, "Mauritania" },
                    { 1217, true, 22, "9598", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9598\", \"nombreMH\": \"MAYOTTE\"}", 156, null, null, null, "Mayotte" },
                    { 1218, true, 22, "9600", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9600\", \"nombreMH\": \"MÉXICO\"}", 157, null, null, null, "México" },
                    { 1219, true, 22, "9601", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9601\", \"nombreMH\": \"MICRONESIA\"}", 158, null, null, null, "Micronesia" },
                    { 1220, true, 22, "9602", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9602\", \"nombreMH\": \"MOLDAVIA\"}", 159, null, null, null, "Moldavia" },
                    { 1221, true, 22, "9603", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9603\", \"nombreMH\": \"MÓNACO\"}", 160, null, null, null, "Mónaco" },
                    { 1222, true, 22, "9606", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9606\", \"nombreMH\": \"MONGOLIA\"}", 161, null, null, null, "Mongolia" },
                    { 1223, true, 22, "9607", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9607\", \"nombreMH\": \"MONTENEGRO\"}", 162, null, null, null, "Montenegro" },
                    { 1224, true, 22, "9608", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9608\", \"nombreMH\": \"MONSERRAT\"}", 163, null, null, null, "Monserrat" },
                    { 1225, true, 22, "9609", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9609\", \"nombreMH\": \"MOZAMBIQUE\"}", 164, null, null, null, "Mozambique" },
                    { 1226, true, 22, "9610", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9610\", \"nombreMH\": \"NAMIBIA\"}", 165, null, null, null, "Namibia" },
                    { 1227, true, 22, "9611", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9611\", \"nombreMH\": \"NAURU\"}", 166, null, null, null, "Nauru" },
                    { 1228, true, 22, "9612", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9612\", \"nombreMH\": \"NEPAL\"}", 167, null, null, null, "Nepal" },
                    { 1229, true, 22, "9615", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9615\", \"nombreMH\": \"NICARAGUA\"}", 168, null, null, null, "Nicaragua" },
                    { 1230, true, 22, "9618", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9618\", \"nombreMH\": \"NÍGER\"}", 169, null, null, null, "Níger" },
                    { 1231, true, 22, "9621", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9621\", \"nombreMH\": \"NIGERIA\"}", 170, null, null, null, "Nigeria" },
                    { 1232, true, 22, "9622", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9622\", \"nombreMH\": \"NIUE\"}", 171, null, null, null, "Niue" },
                    { 1233, true, 22, "9623", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9623\", \"nombreMH\": \"NORFOLK\"}", 172, null, null, null, "Norfolk" },
                    { 1234, true, 22, "9624", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9624\", \"nombreMH\": \"NORUEGA\"}", 173, null, null, null, "Noruega" },
                    { 1235, true, 22, "9627", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9627\", \"nombreMH\": \"NVA CALEDONIA\"}", 174, null, null, null, "Nva Caledonia" },
                    { 1236, true, 22, "9633", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9633\", \"nombreMH\": \"NVA ZELANDIA\"}", 175, null, null, null, "Nva Zelandia" },
                    { 1237, true, 22, "9636", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9636\", \"nombreMH\": \"NUEVAS HEBRIDAS\"}", 176, null, null, null, "Nuevas Hebridas" },
                    { 1238, true, 22, "9638", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9638\", \"nombreMH\": \"PAPUA NV GUINEA\"}", 177, null, null, null, "Papua Nv Guinea" },
                    { 1239, true, 22, "9639", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9639\", \"nombreMH\": \"PAKISTÁN\"}", 178, null, null, null, "Pakistán" },
                    { 1240, true, 22, "9640", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9640\", \"nombreMH\": \"PALESTINA\"}", 179, null, null, null, "Palestina" },
                    { 1241, true, 22, "9641", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9641\", \"nombreMH\": \"CROACIA\"}", 180, null, null, null, "Croacia" },
                    { 1242, true, 22, "9642", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9642\", \"nombreMH\": \"PANAMÁ\"}", 181, null, null, null, "Panamá" },
                    { 1243, true, 22, "9643", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9643\", \"nombreMH\": \"PALAOS\"}", 182, null, null, null, "Palaos" },
                    { 1244, true, 22, "9645", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9645\", \"nombreMH\": \"PARAGUAY\"}", 183, null, null, null, "Paraguay" },
                    { 1245, true, 22, "9648", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9648\", \"nombreMH\": \"PERÚ\"}", 184, null, null, null, "Perú" },
                    { 1246, true, 22, "9651", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9651\", \"nombreMH\": \"POLONIA\"}", 185, null, null, null, "Polonia" },
                    { 1247, true, 22, "9652", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9652\", \"nombreMH\": \"POLINESIA FRANCESA\"}", 186, null, null, null, "Polinesia Francesa" },
                    { 1248, true, 22, "9654", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9654\", \"nombreMH\": \"PORTUGAL\"}", 187, null, null, null, "Portugal" },
                    { 1249, true, 22, "9660", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9660\", \"nombreMH\": \"QATAR\"}", 188, null, null, null, "Qatar" },
                    { 1250, true, 22, "9663", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9663\", \"nombreMH\": \"EL REINO UNIDO\"}", 189, null, null, null, "El Reino Unido" },
                    { 1251, true, 22, "9664", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9664\", \"nombreMH\": \"REPUBLICA CHECA\"}", 190, null, null, null, "Republica Checa" },
                    { 1252, true, 22, "9666", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9666\", \"nombreMH\": \"EGIPTO\"}", 191, null, null, null, "Egipto" },
                    { 1253, true, 22, "9667", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9667\", \"nombreMH\": \"REUNIÓN\"}", 192, null, null, null, "Reunión" },
                    { 1254, true, 22, "9669", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9669\", \"nombreMH\": \"RODESIA\"}", 193, null, null, null, "Rodesia" },
                    { 1255, true, 22, "9672", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9672\", \"nombreMH\": \"RUANDA\"}", 194, null, null, null, "Ruanda" },
                    { 1256, true, 22, "9673", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9673\", \"nombreMH\": \"REPUBLICA DE ARMENIA\"}", 195, null, null, null, "Republica De Armenia" },
                    { 1257, true, 22, "9675", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9675\", \"nombreMH\": \"RUMANIA\"}", 196, null, null, null, "Rumania" },
                    { 1258, true, 22, "9676", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9676\", \"nombreMH\": \"SAHARA OCCIDENTAL\"}", 197, null, null, null, "Sahara Occidental" },
                    { 1259, true, 22, "9677", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9677\", \"nombreMH\": \"SAN MARINO\"}", 198, null, null, null, "San Marino" },
                    { 1260, true, 22, "9678", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9678\", \"nombreMH\": \"SAMOA OCCID\"}", 199, null, null, null, "Samoa Occid" },
                    { 1261, true, 22, "9679", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9679\", \"nombreMH\": \"SAINT KITTS AND NEVIS\"}", 200, null, null, null, "Saint Kitts And Nevis" },
                    { 1262, true, 22, "9680", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9680\", \"nombreMH\": \"SANTA LUCIA\"}", 201, null, null, null, "Santa Lucia" },
                    { 1263, true, 22, "9681", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9681\", \"nombreMH\": \"SENEGAL\"}", 202, null, null, null, "Senegal" },
                    { 1264, true, 22, "9682", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9682\", \"nombreMH\": \"SAOTOME Y PRINC\"}", 203, null, null, null, "Saotome Y Princ" },
                    { 1265, true, 22, "9683", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9683\", \"nombreMH\": \"SN VIC Y GRENAD\"}", 204, null, null, null, "Sn Vic Y Grenad" },
                    { 1266, true, 22, "9684", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9684\", \"nombreMH\": \"SIERRA LEONA\"}", 205, null, null, null, "Sierra Leona" },
                    { 1267, true, 22, "9685", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9685\", \"nombreMH\": \"SAMOA AMERICANA\"}", 206, null, null, null, "Samoa Americana" },
                    { 1268, true, 22, "9686", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9686\", \"nombreMH\": \"SAN PEDRO Y MIQUELÓN\"}", 207, null, null, null, "San Pedro Y Miquelón" },
                    { 1269, true, 22, "9687", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9687\", \"nombreMH\": \"SINGAPUR\"}", 208, null, null, null, "Singapur" },
                    { 1270, true, 22, "9688", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9688\", \"nombreMH\": \"SANTA ELENA\"}", 209, null, null, null, "Santa Elena" },
                    { 1271, true, 22, "9689", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9689\", \"nombreMH\": \"SERBIA\"}", 210, null, null, null, "Serbia" },
                    { 1272, true, 22, "9690", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9690\", \"nombreMH\": \"SIRIA\"}", 211, null, null, null, "Siria" },
                    { 1273, true, 22, "9691", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9691\", \"nombreMH\": \"SEYCHELLES\"}", 212, null, null, null, "Seychelles" },
                    { 1274, true, 22, "9692", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9692\", \"nombreMH\": \"SVALBARD Y JAN MAYEN\"}", 213, null, null, null, "Svalbard Y Jan Mayen" },
                    { 1275, true, 22, "9693", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9693\", \"nombreMH\": \"SOMALIA\"}", 214, null, null, null, "Somalia" },
                    { 1276, true, 22, "9696", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9696\", \"nombreMH\": \"SUDÁFRICA REP\"}", 215, null, null, null, "Sudáfrica Rep" },
                    { 1277, true, 22, "9699", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9699\", \"nombreMH\": \"SUDAN\"}", 216, null, null, null, "Sudan" },
                    { 1278, true, 22, "9702", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9702\", \"nombreMH\": \"SUECIA\"}", 217, null, null, null, "Suecia" },
                    { 1279, true, 22, "9705", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9705\", \"nombreMH\": \"SUIZA\"}", 218, null, null, null, "Suiza" },
                    { 1280, true, 22, "9706", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9706\", \"nombreMH\": \"SURINAM\"}", 219, null, null, null, "Surinam" },
                    { 1281, true, 22, "9707", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9707\", \"nombreMH\": \"SRI LANKA\"}", 220, null, null, null, "Sri Lanka" },
                    { 1282, true, 22, "9708", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9708\", \"nombreMH\": \"SUECILANDIA\"}", 221, null, null, null, "Suecilandia" },
                    { 1283, true, 22, "9709", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9709\", \"nombreMH\": \"TAYIKISTÁN\"}", 222, null, null, null, "Tayikistán" },
                    { 1284, true, 22, "9711", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9711\", \"nombreMH\": \"TAILANDIA\"}", 223, null, null, null, "Tailandia" },
                    { 1285, true, 22, "9712", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9712\", \"nombreMH\": \"TERRITORIO BRITÁNICO DEL OCÉANO INDICO\"}", 224, null, null, null, "Territorio Británico Del Océano Indico" },
                    { 1286, true, 22, "9713", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9713\", \"nombreMH\": \"TERRITORIOS AUSTRALES FRANCESES\"}", 225, null, null, null, "Territorios Australes Franceses" },
                    { 1287, true, 22, "9714", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9714\", \"nombreMH\": \"TANZANIA\"}", 226, null, null, null, "Tanzania" },
                    { 1288, true, 22, "9715", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9715\", \"nombreMH\": \"TERRITORIOS PALESTINOS\"}", 227, null, null, null, "Territorios Palestinos" },
                    { 1289, true, 22, "9716", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9716\", \"nombreMH\": \"TIMOR ORIENTAL\"}", 228, null, null, null, "Timor Oriental" },
                    { 1290, true, 22, "9717", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9717\", \"nombreMH\": \"TOGO\"}", 229, null, null, null, "Togo" },
                    { 1291, true, 22, "9718", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9718\", \"nombreMH\": \"TOKELAU\"}", 230, null, null, null, "Tokelau" },
                    { 1292, true, 22, "9719", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9719\", \"nombreMH\": \"TURKMENISTÁN\"}", 231, null, null, null, "Turkmenistán" },
                    { 1293, true, 22, "9720", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9720\", \"nombreMH\": \"TRINIDAD TOBAGO\"}", 232, null, null, null, "Trinidad Tobago" },
                    { 1294, true, 22, "9722", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9722\", \"nombreMH\": \"TONGA\"}", 233, null, null, null, "Tonga" },
                    { 1295, true, 22, "9723", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9723\", \"nombreMH\": \"TÚNEZ\"}", 234, null, null, null, "Túnez" },
                    { 1296, true, 22, "9725", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9725\", \"nombreMH\": \"TRANSKEI\"}", 235, null, null, null, "Transkei" },
                    { 1297, true, 22, "9726", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9726\", \"nombreMH\": \"TURQUÍA\"}", 236, null, null, null, "Turquía" },
                    { 1298, true, 22, "9727", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9727\", \"nombreMH\": \"TUVALU\"}", 237, null, null, null, "Tuvalu" },
                    { 1299, true, 22, "9729", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9729\", \"nombreMH\": \"UGANDA\"}", 238, null, null, null, "Uganda" },
                    { 1300, true, 22, "9732", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9732\", \"nombreMH\": \"URSS\"}", 239, null, null, null, "Urss" },
                    { 1301, true, 22, "9733", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9733\", \"nombreMH\": \"RUSIA\"}", 240, null, null, null, "Rusia" },
                    { 1302, true, 22, "9735", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9735\", \"nombreMH\": \"URUGUAY\"}", 241, null, null, null, "Uruguay" },
                    { 1303, true, 22, "9736", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9736\", \"nombreMH\": \"UCRANIA\"}", 242, null, null, null, "Ucrania" },
                    { 1304, true, 22, "9737", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9737\", \"nombreMH\": \"UZBEKISTÁN\"}", 243, null, null, null, "Uzbekistán" },
                    { 1305, true, 22, "9738", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9738\", \"nombreMH\": \"VATICANO\"}", 244, null, null, null, "Vaticano" },
                    { 1306, true, 22, "9739", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9739\", \"nombreMH\": \"VANUATU\"}", 245, null, null, null, "Vanuatu" },
                    { 1307, true, 22, "9740", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9740\", \"nombreMH\": \"VENDA\"}", 246, null, null, null, "Venda" },
                    { 1308, true, 22, "9741", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9741\", \"nombreMH\": \"VENEZUELA\"}", 247, null, null, null, "Venezuela" },
                    { 1309, true, 22, "9744", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9744\", \"nombreMH\": \"VIETNAM NORTE\"}", 248, null, null, null, "Vietnam Norte" },
                    { 1310, true, 22, "9746", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9746\", \"nombreMH\": \"VIETNAM\"}", 249, null, null, null, "Vietnam" },
                    { 1311, true, 22, "9747", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9747\", \"nombreMH\": \"VIETNAM SUR\"}", 250, null, null, null, "Vietnam Sur" },
                    { 1312, true, 22, "9750", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9750\", \"nombreMH\": \"YEMEN SUR\"}", 251, null, null, null, "Yemen Sur" },
                    { 1313, true, 22, "9751", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9751\", \"nombreMH\": \"YIBUTI\"}", 252, null, null, null, "Yibuti" },
                    { 1314, true, 22, "9756", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9756\", \"nombreMH\": \"REP YUGOSLAVIA\"}", 253, null, null, null, "Rep Yugoslavia" },
                    { 1315, true, 22, "9758", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9758\", \"nombreMH\": \"ZAIRE\"}", 254, null, null, null, "Zaire" },
                    { 1316, true, 22, "9759", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9759\", \"nombreMH\": \"ZAMBIA\"}", 255, null, null, null, "Zambia" },
                    { 1317, true, 22, "9760", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9760\", \"nombreMH\": \"ZIMBABWE\"}", 256, null, null, null, "Zimbabwe" },
                    { 1318, true, 22, "9850", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9850\", \"nombreMH\": \"PUERTO RICO\"}", 257, null, null, null, "Puerto Rico" },
                    { 1319, true, 22, "9862", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9862\", \"nombreMH\": \"BAHAMAS\"}", 258, null, null, null, "Bahamas" },
                    { 1320, true, 22, "9863", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9863\", \"nombreMH\": \"BERMUDAS\"}", 259, null, null, null, "Bermudas" },
                    { 1321, true, 22, "9865", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9865\", \"nombreMH\": \"MARTINICA\"}", 260, null, null, null, "Martinica" },
                    { 1322, true, 22, "9886", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9886\", \"nombreMH\": \"NUEVA GUINEA\"}", 261, null, null, null, "Nueva Guinea" },
                    { 1323, true, 22, "9887", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9887\", \"nombreMH\": \"ISLAS GRAN CAIMÁN\"}", 262, null, null, null, "Islas Gran Caimán" },
                    { 1324, true, 22, "9888", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9888\", \"nombreMH\": \"SAN MAARTEN\"}", 263, null, null, null, "San Maarten" },
                    { 1325, true, 22, "9897", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9897\", \"nombreMH\": \"ISLAS VÍRGENES BRITÁNICAS\"}", 264, null, null, null, "Islas Vírgenes Británicas" },
                    { 1326, true, 22, "9898", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9898\", \"nombreMH\": \"ANT HOLANDESAS\"}", 265, null, null, null, "Ant Holandesas" },
                    { 1327, true, 22, "9899", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9899\", \"nombreMH\": \"TAIWÁN\"}", 266, null, null, null, "Taiwán" },
                    { 1328, true, 22, "9900", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9900\", \"nombreMH\": \"DELAWARE (USA)\"}", 267, null, null, null, "Delaware (Usa)" },
                    { 1329, true, 22, "9901", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9901\", \"nombreMH\": \"NEVADA (USA)\"}", 268, null, null, null, "Nevada (Usa)" },
                    { 1330, true, 22, "9902", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9902\", \"nombreMH\": \"WYOMING (USA)\"}", 269, null, null, null, "Wyoming (Usa)" },
                    { 1331, true, 22, "9903", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9903\", \"nombreMH\": \"CAMPIONE D'ITALIA, ITALIA\"}", 270, null, null, null, "Campione D'Italia, Italia" },
                    { 1332, true, 22, "9904", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9904\", \"nombreMH\": \"FLORIDA (USA)\"}", 271, null, null, null, "Florida (Usa)" },
                    { 1333, true, 22, "9905", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9905\", \"nombreMH\": \"DAKOTA DEL SUR (USA)\"}", 272, null, null, null, "Dakota Del Sur (Usa)" },
                    { 1334, true, 22, "9906", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9906\", \"nombreMH\": \"TEXAS (USA)\"}", 273, null, null, null, "Texas (Usa)" },
                    { 1335, true, 22, "9907", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9907\", \"nombreMH\": \"WASHINGTON (USA)\"}", 274, null, null, null, "Washington (Usa)" },
                    { 1336, true, 22, "9999", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9999\", \"nombreMH\": \"No definido en migración\"}", 275, null, null, null, "No definido en migración" },
                    { 1386, true, 7, "36", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"36\"}", 1, null, null, null, "NIT" },
                    { 1387, true, 7, "13", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"13\"}", 2, null, null, null, "DUI" },
                    { 1388, true, 7, "37", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"37\"}", 3, null, null, null, "Otro" },
                    { 1389, true, 7, "03", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"03\"}", 4, null, null, null, "Pasaporte" },
                    { 1390, true, 7, "02", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"02\"}", 5, null, null, null, "Carnet de Residente" },
                    { 1398, true, 23, "1", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"1\"}", 1, null, null, null, "Error en la información del Documento Tributario Electrónico a invalidar." },
                    { 1399, true, 23, "2", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"2\"}", 2, null, null, null, "Rescindir de la operación realizada." },
                    { 1400, true, 23, "3", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"3\"}", 3, null, null, null, "Otro" }
                });

            migrationBuilder.InsertData(
                table: "Core_Catalogos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EmpresaId", "EsSistema", "MetadataJson", "Nombre", "UpdatedAt", "UpdatedBy", "Version" },
                values: new object[,]
                {
                    { 26, true, "RETENCION_IVA_MH", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-006 Hacienda. 3 ítems oficiales.", null, true, null, "Retención IVA MH", null, null, 1 },
                    { 27, true, "PLAZO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-018 Hacienda. Días / Meses / Años.", null, true, null, "Plazo", null, null, 1 },
                    { 28, true, "OTRO_DOC_ASOCIADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-021 Hacienda. 4 ítems oficiales.", null, true, null, "Otros Documentos Asociados", null, null, 1 },
                    { 29, true, "TIPO_DOC_CONTINGENCIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-023 Hacienda. 7 tipos.", null, true, null, "Tipo Documento en Contingencia", null, null, 1 },
                    { 30, true, "TITULO_REMISION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-025 Hacienda. 5 títulos.", null, true, null, "Título Remisión de Bienes", null, null, 1 },
                    { 31, true, "TIPO_DONACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-026 Hacienda. Efectivo / Bien / Servicio.", null, true, null, "Tipo de Donación", null, null, 1 },
                    { 32, true, "RECINTO_FISCAL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-027 Hacienda. 45 recintos (incluye Z.F. EMCO y Z.F. Gigante).", null, true, null, "Recinto Fiscal", null, null, 1 },
                    { 33, true, "TIPO_PERSONA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-029 Hacienda. Natural / Jurídica.", null, true, null, "Tipo de Persona", null, null, 1 },
                    { 34, true, "TRANSPORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-030 Hacienda. 7 modalidades.", null, true, null, "Transporte", null, null, 1 },
                    { 35, true, "INCOTERMS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-031 Hacienda. 16 términos comerciales internacionales.", null, true, null, "INCOTERMS", null, null, 1 },
                    { 36, true, "DOMICILIO_FISCAL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CAT-032 Hacienda. Domiciliado / No Domiciliado.", null, true, null, "Domicilio Fiscal", null, null, 1 }
                });

            migrationBuilder.InsertData(
                table: "Core_CatalogoItems",
                columns: new[] { "Id", "Activo", "CatalogoId", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EsSistema", "MetadataJson", "Orden", "ParentCodigo", "UpdatedAt", "UpdatedBy", "Valor" },
                values: new object[,]
                {
                    { 1000, true, 26, "22", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"22\"}", 1, null, null, null, "Retención IVA 1%" },
                    { 1001, true, 26, "C4", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"C4\"}", 2, null, null, null, "Retención IVA 13%" },
                    { 1002, true, 26, "C9", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"C9\"}", 3, null, null, null, "Otras retenciones IVA casos especiales" },
                    { 1059, true, 27, "01", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"01\"}", 1, null, null, null, "Días" },
                    { 1060, true, 27, "02", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"02\"}", 2, null, null, null, "Meses" },
                    { 1061, true, 27, "03", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"03\"}", 3, null, null, null, "Años" },
                    { 1337, true, 32, "01", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"01\"}", 1, null, null, null, "Terrestre San Bartolo" },
                    { 1338, true, 32, "02", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"02\"}", 2, null, null, null, "Marítima de Acajutla" },
                    { 1339, true, 32, "03", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"03\"}", 3, null, null, null, "Aérea Monseñor Óscar Arnulfo Romero" },
                    { 1340, true, 32, "04", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"04\"}", 4, null, null, null, "Terrestre Las Chinamas" },
                    { 1341, true, 32, "05", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"05\"}", 5, null, null, null, "Terrestre La Hachadura" },
                    { 1342, true, 32, "06", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"06\"}", 6, null, null, null, "Terrestre Santa Ana" },
                    { 1343, true, 32, "07", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"07\"}", 7, null, null, null, "Terrestre San Cristóbal" },
                    { 1344, true, 32, "08", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"08\"}", 8, null, null, null, "Terrestre Anguiatú" },
                    { 1345, true, 32, "09", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"09\"}", 9, null, null, null, "Terrestre El Amatillo" },
                    { 1346, true, 32, "10", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"10\"}", 10, null, null, null, "Marítima La Unión (Puerto Cutuco)" },
                    { 1347, true, 32, "11", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"11\"}", 11, null, null, null, "Terrestre El Poy" },
                    { 1348, true, 32, "12", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"12\"}", 12, null, null, null, "Aduana Terrestre Metalío" },
                    { 1349, true, 32, "15", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"15\"}", 13, null, null, null, "Fardos Postales" },
                    { 1350, true, 32, "16", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"16\"}", 14, null, null, null, "Z.F. San Marcos" },
                    { 1351, true, 32, "17", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"17\"}", 15, null, null, null, "Z.F. El Pedregal" },
                    { 1352, true, 32, "18", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"18\"}", 16, null, null, null, "Z.F. San Bartolo" },
                    { 1353, true, 32, "20", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"20\"}", 17, null, null, null, "Z.F. Exportsalva" },
                    { 1354, true, 32, "21", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"21\"}", 18, null, null, null, "Z.F. American Park" },
                    { 1355, true, 32, "23", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"23\"}", 19, null, null, null, "Z.F. Internacional" },
                    { 1356, true, 32, "24", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"24\"}", 20, null, null, null, "Z.F. Diez" },
                    { 1357, true, 32, "26", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"26\"}", 21, null, null, null, "Z.F. Miramar" },
                    { 1358, true, 32, "27", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"27\"}", 22, null, null, null, "Z.F. Santo Tomas" },
                    { 1359, true, 32, "28", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"28\"}", 23, null, null, null, "Z.F. Santa Tecla" },
                    { 1360, true, 32, "29", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"29\"}", 24, null, null, null, "Z.F. Santa Ana" },
                    { 1361, true, 32, "30", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"30\"}", 25, null, null, null, "Z.F. La Concordia" },
                    { 1362, true, 32, "31", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"31\"}", 26, null, null, null, "Aérea Ilopango" },
                    { 1363, true, 32, "32", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"32\"}", 27, null, null, null, "Z.F. Pipil" },
                    { 1364, true, 32, "33", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"33\"}", 28, null, null, null, "Puerto Barillas" },
                    { 1365, true, 32, "34", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"34\"}", 29, null, null, null, "Z.F. Calvo Conservas" },
                    { 1366, true, 32, "35", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"35\"}", 30, null, null, null, "Feria Internacional" },
                    { 1367, true, 32, "36", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"36\"}", 31, null, null, null, "Delg. Aduana El Papalón" },
                    { 1368, true, 32, "37", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"37\"}", 32, null, null, null, "Z.F. Parque Industrial Sam-Li" },
                    { 1369, true, 32, "38", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"38\"}", 33, null, null, null, "Z.F. San José" },
                    { 1370, true, 32, "39", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"39\"}", 34, null, null, null, "Z.F. Las Mercedes" },
                    { 1371, true, 32, "40", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"40\"}", 35, null, null, null, "Z.F. EMCO" },
                    { 1372, true, 32, "41", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"41\"}", 36, null, null, null, "Z.F. Gigante" },
                    { 1373, true, 32, "71", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"71\"}", 37, null, null, null, "Almacenes De Desarrollo (Aldesa)" },
                    { 1374, true, 32, "72", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"72\"}", 38, null, null, null, "Almac. Gral. Dep. Occidente (Agdosa)" },
                    { 1375, true, 32, "73", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"73\"}", 39, null, null, null, "Bodega General De Depósito (Bodesa)" },
                    { 1376, true, 32, "76", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"76\"}", 40, null, null, null, "DHL" },
                    { 1377, true, 32, "77", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"77\"}", 41, null, null, null, "Transauto (Santa Elena)" },
                    { 1378, true, 32, "80", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"80\"}", 42, null, null, null, "Almacenadora Nejapa, S.A. de C.V." },
                    { 1379, true, 32, "81", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"81\"}", 43, null, null, null, "Almacenadora Almaconsa S.A. de C.V." },
                    { 1380, true, 32, "83", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"83\"}", 44, null, null, null, "Alm.Gral. Depósito Occidente (Apopa)" },
                    { 1381, true, 32, "99", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"99\"}", 45, null, null, null, "San Bartolo Envío Hn/Gt" },
                    { 1382, true, 28, "1", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"1\"}", 1, null, null, null, "Emisor" },
                    { 1383, true, 28, "2", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"2\"}", 2, null, null, null, "Receptor" },
                    { 1384, true, 28, "3", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"3\"}", 3, null, null, null, "Médico (solo aplica para contribuyentes obligados a la presentación de F-958)" },
                    { 1385, true, 28, "4", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"4\"}", 4, null, null, null, "Transporte (solo aplica para Factura de Exportación)" },
                    { 1391, true, 29, "01", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"01\"}", 1, null, null, null, "Factura Electrónica" },
                    { 1392, true, 29, "03", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"03\"}", 2, null, null, null, "Comprobante de Crédito Fiscal Electrónico" },
                    { 1393, true, 29, "04", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"04\"}", 3, null, null, null, "Nota de Remisión Electrónica" },
                    { 1394, true, 29, "05", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"05\"}", 4, null, null, null, "Nota de Crédito Electrónica" },
                    { 1395, true, 29, "06", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"06\"}", 5, null, null, null, "Nota de Débito Electrónica" },
                    { 1396, true, 29, "11", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"11\"}", 6, null, null, null, "Factura de Exportación Electrónica" },
                    { 1397, true, 29, "14", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"14\"}", 7, null, null, null, "Factura de Sujeto Excluido Electrónica" },
                    { 1401, true, 30, "01", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"01\"}", 1, null, null, null, "Depósito" },
                    { 1402, true, 30, "02", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"02\"}", 2, null, null, null, "Propiedad" },
                    { 1403, true, 30, "03", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"03\"}", 3, null, null, null, "Consignación" },
                    { 1404, true, 30, "04", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"04\"}", 4, null, null, null, "Traslado" },
                    { 1405, true, 30, "05", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"05\"}", 5, null, null, null, "Otros" },
                    { 1406, true, 31, "1", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"1\"}", 1, null, null, null, "Efectivo" },
                    { 1407, true, 31, "2", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"2\"}", 2, null, null, null, "Bien" },
                    { 1408, true, 31, "3", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"3\"}", 3, null, null, null, "Servicio" },
                    { 1409, true, 33, "1", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"1\"}", 1, null, null, null, "Persona Natural" },
                    { 1410, true, 33, "2", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"2\"}", 2, null, null, null, "Persona Jurídica" },
                    { 1411, true, 34, "1", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"1\"}", 1, null, null, null, "Terrestre" },
                    { 1412, true, 34, "2", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"2\"}", 2, null, null, null, "Marítimo" },
                    { 1413, true, 34, "3", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"3\"}", 3, null, null, null, "Aéreo" },
                    { 1414, true, 34, "4", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"4\"}", 4, null, null, null, "Multimodal, Terrestre-marítimo" },
                    { 1415, true, 34, "5", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"5\"}", 5, null, null, null, "Multimodal, Terrestre-aéreo" },
                    { 1416, true, 34, "6", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"6\"}", 6, null, null, null, "Multimodal, Marítimo-aéreo" },
                    { 1417, true, 34, "7", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"7\"}", 7, null, null, null, "Multimodal, Terrestre-Marítimo-aéreo" },
                    { 1418, true, 35, "01", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"01\"}", 1, null, null, null, "EXW-En fábrica" },
                    { 1419, true, 35, "02", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"02\"}", 2, null, null, null, "FCA-Libre transportista" },
                    { 1420, true, 35, "03", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"03\"}", 3, null, null, null, "CPT-Transporte pagado hasta" },
                    { 1421, true, 35, "04", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"04\"}", 4, null, null, null, "CIP-Transporte y seguro pagado hasta" },
                    { 1422, true, 35, "05", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"05\"}", 5, null, null, null, "DAP-Entrega en el lugar" },
                    { 1423, true, 35, "06", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"06\"}", 6, null, null, null, "DPU-Entregado en el lugar descargado" },
                    { 1424, true, 35, "07", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"07\"}", 7, null, null, null, "DDP-Entrega con impuestos pagados" },
                    { 1425, true, 35, "08", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"08\"}", 8, null, null, null, "FAS-Libre al costado del buque" },
                    { 1426, true, 35, "09", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"09\"}", 9, null, null, null, "FOB-Libre a bordo" },
                    { 1427, true, 35, "10", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"10\"}", 10, null, null, null, "CFR-Costo y flete" },
                    { 1428, true, 35, "11", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"11\"}", 11, null, null, null, "CIF-Costo seguro y flete" },
                    { 1429, true, 35, "12", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"12\"}", 12, null, null, null, "DAT-Entregado en terminal" },
                    { 1430, true, 35, "13", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"13\"}", 13, null, null, null, "DAF-Entregada en frontera" },
                    { 1431, true, 35, "14", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"14\"}", 14, null, null, null, "DES-Entregada sobre buque" },
                    { 1432, true, 35, "15", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"15\"}", 15, null, null, null, "DEQ-Entregada en muelle" },
                    { 1433, true, 35, "16", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"16\"}", 16, null, null, null, "DDU-Entregada derechos no pagados" },
                    { 1434, true, 36, "1", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"1\"}", 1, null, null, null, "Domiciliado" },
                    { 1435, true, 36, "2", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"2\"}", 2, null, null, null, "No Domiciliado" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1000);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1004);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1005);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1006);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1007);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1008);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1009);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1010);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1011);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1012);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1013);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1014);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1015);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1016);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1017);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1018);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1019);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1020);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1021);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1022);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1023);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1024);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1025);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1026);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1027);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1028);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1029);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1030);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1031);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1032);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1033);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1034);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1035);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1036);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1037);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1038);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1039);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1040);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1041);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1042);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1043);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1044);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1045);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1046);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1047);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1048);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1049);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1050);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1051);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1052);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1053);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1054);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1055);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1056);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1057);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1058);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1059);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1060);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1061);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1062);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1063);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1064);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1065);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1066);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1067);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1068);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1069);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1070);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1071);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1072);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1073);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1074);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1075);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1076);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1077);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1078);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1079);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1080);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1081);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1082);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1083);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1084);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1085);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1086);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1087);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1088);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1089);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1090);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1091);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1092);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1093);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1094);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1095);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1096);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1097);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1098);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1099);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1100);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1101);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1102);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1103);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1104);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1105);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1106);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1107);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1108);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1109);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1110);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1111);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1112);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1113);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1114);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1115);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1116);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1117);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1118);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1119);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1120);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1121);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1122);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1123);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1124);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1125);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1126);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1127);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1128);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1129);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1130);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1131);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1132);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1133);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1134);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1135);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1136);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1137);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1138);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1139);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1140);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1141);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1142);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1143);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1144);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1145);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1146);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1147);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1148);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1149);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1150);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1151);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1152);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1153);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1154);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1155);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1156);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1157);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1158);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1159);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1160);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1161);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1162);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1163);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1164);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1165);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1166);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1167);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1168);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1169);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1170);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1171);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1172);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1173);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1174);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1175);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1176);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1177);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1178);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1179);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1180);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1181);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1182);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1183);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1184);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1185);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1186);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1187);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1188);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1189);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1190);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1191);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1192);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1193);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1194);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1195);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1196);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1197);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1198);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1199);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1200);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1201);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1202);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1203);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1204);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1205);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1206);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1207);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1208);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1209);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1210);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1211);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1212);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1213);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1214);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1215);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1216);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1217);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1218);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1219);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1220);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1221);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1222);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1223);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1224);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1225);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1226);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1227);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1228);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1229);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1230);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1231);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1232);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1233);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1234);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1235);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1236);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1237);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1238);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1239);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1240);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1241);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1242);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1243);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1244);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1245);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1246);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1247);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1248);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1249);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1250);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1251);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1252);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1253);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1254);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1255);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1256);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1257);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1258);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1259);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1260);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1261);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1262);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1263);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1264);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1265);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1266);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1267);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1268);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1269);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1270);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1271);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1272);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1273);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1274);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1275);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1276);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1277);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1278);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1279);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1280);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1281);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1282);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1283);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1284);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1285);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1286);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1287);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1288);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1289);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1290);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1291);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1292);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1293);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1294);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1295);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1296);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1297);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1298);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1299);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1300);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1301);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1302);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1303);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1304);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1305);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1306);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1307);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1308);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1309);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1310);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1311);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1312);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1313);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1314);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1315);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1316);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1317);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1318);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1319);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1320);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1321);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1322);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1323);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1324);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1325);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1326);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1327);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1328);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1329);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1330);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1331);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1332);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1333);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1334);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1335);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1336);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1337);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1338);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1339);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1340);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1341);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1342);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1343);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1344);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1345);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1346);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1347);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1348);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1349);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1350);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1351);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1352);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1353);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1354);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1355);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1356);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1357);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1358);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1359);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1360);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1361);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1362);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1363);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1364);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1365);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1366);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1367);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1368);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1369);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1370);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1371);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1372);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1373);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1374);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1375);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1376);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1377);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1378);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1379);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1380);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1381);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1382);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1383);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1384);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1385);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1386);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1387);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1388);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1389);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1390);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1391);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1392);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1393);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1394);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1395);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1396);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1397);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1398);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1399);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1400);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1401);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1402);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1403);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1404);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1405);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1406);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1407);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1408);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1409);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1410);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1411);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1412);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1413);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1414);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1415);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1416);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1417);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1418);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1419);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1420);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1421);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1422);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1423);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1424);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1425);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1426);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1427);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1428);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1429);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1430);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1431);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1432);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1433);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1434);

            migrationBuilder.DeleteData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1435);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Core_Catalogos",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.InsertData(
                table: "Core_CatalogoItems",
                columns: new[] { "Id", "Activo", "CatalogoId", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EsSistema", "MetadataJson", "Orden", "ParentCodigo", "UpdatedAt", "UpdatedBy", "Valor" },
                values: new object[,]
                {
                    { 38, true, 7, "DUI", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, null, "DUI" },
                    { 39, true, 7, "NIT", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, null, "NIT" },
                    { 40, true, 7, "PASAPORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, null, "Pasaporte" },
                    { 41, true, 7, "CARNET_RESIDENTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, null, "Carnet de Residente" },
                    { 42, true, 7, "OTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, null, "Otro documento" },
                    { 85, true, 15, "UNIDAD", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"59\"}", 1, null, null, null, "Unidad" },
                    { 86, true, 15, "CAJA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"43\"}", 2, null, null, null, "Caja" },
                    { 87, true, 15, "DOCENA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"44\"}", 3, null, null, null, "Docena" },
                    { 88, true, 15, "LITRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"22\"}", 4, null, null, null, "Litro" },
                    { 89, true, 15, "GALON", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"23\"}", 5, null, null, null, "Galón" },
                    { 90, true, 15, "KILOGRAMO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"10\"}", 6, null, null, null, "Kilogramo" },
                    { 91, true, 15, "LIBRA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"15\"}", 7, null, null, null, "Libra" },
                    { 92, true, 15, "METRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"32\"}", 8, null, null, null, "Metro" },
                    { 93, true, 15, "SERVICIO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"58\"}", 9, null, null, null, "Servicio" },
                    { 94, true, 15, "HORA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"56\"}", 10, null, null, null, "Hora" },
                    { 95, true, 15, "DIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"55\"}", 11, null, null, null, "Día" },
                    { 96, true, 15, "PAQUETE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"49\"}", 12, null, null, null, "Paquete" },
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
                    { 204, true, 23, "OTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"3\"}", 3, null, null, null, "Otros (justificar en detalle)" }
                });
        }
    }
}
