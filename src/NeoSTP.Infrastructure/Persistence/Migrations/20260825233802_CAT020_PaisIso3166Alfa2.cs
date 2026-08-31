using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CAT020_PaisIso3166Alfa2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1062,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AF", "{\"codigoMH\": \"AF\"}", "Afganistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1063,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AX", "{\"codigoMH\": \"AX\"}", "Aland" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1064,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AL", "{\"codigoMH\": \"AL\"}", "Albania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1065,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "DE", "{\"codigoMH\": \"DE\"}", "Alemania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1066,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AD", "{\"codigoMH\": \"AD\"}", "Andorra" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1067,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AO", "{\"codigoMH\": \"AO\"}", "Angola" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1068,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AI", "{\"codigoMH\": \"AI\"}", "Anguila" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1069,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AQ", "{\"codigoMH\": \"AQ\"}", "Antártica" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1070,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AG", "{\"codigoMH\": \"AG\"}", "Antigua y Barbuda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1071,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AW", "{\"codigoMH\": \"AW\"}", "Aruba" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1072,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SA", "{\"codigoMH\": \"SA\"}", "Arabia Saudita" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1073,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "DZ", "{\"codigoMH\": \"DZ\"}", "Argelia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1074,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AR", "{\"codigoMH\": \"AR\"}", "Argentina" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1075,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AM", "{\"codigoMH\": \"AM\"}", "Armenia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1076,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AU", "{\"codigoMH\": \"AU\"}", "Australia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1077,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AT", "{\"codigoMH\": \"AT\"}", "Austria" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1078,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AZ", "{\"codigoMH\": \"AZ\"}", "Azerbaiyán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1079,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BS", "{\"codigoMH\": \"BS\"}", "Bahamas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1080,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BH", "{\"codigoMH\": \"BH\"}", "Bahrein" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1081,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "BD", "{\"codigoMH\": \"BD\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1082,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BB", "{\"codigoMH\": \"BB\"}", "Barbados" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1083,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BE", "{\"codigoMH\": \"BE\"}", "Bélgica" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1084,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BZ", "{\"codigoMH\": \"BZ\"}", "Belice" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1085,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BJ", "{\"codigoMH\": \"BJ\"}", "Benin" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1086,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BM", "{\"codigoMH\": \"BM\"}", "Bermudas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1087,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "BY", "{\"codigoMH\": \"BY\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1088,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BO", "{\"codigoMH\": \"BO\"}", "Bolivia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1089,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BQ", "{\"codigoMH\": \"BQ\"}", "Bonaire, Sint Eustatius and Saba" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1090,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BA", "{\"codigoMH\": \"BA\"}", "Bosnia-Herzegovina" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1091,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "BW", "{\"codigoMH\": \"BW\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1092,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "BR", "{\"codigoMH\": \"BR\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1093,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BN", "{\"codigoMH\": \"BN\"}", "Brunei" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1094,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "BG", "{\"codigoMH\": \"BG\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1095,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "BF", "{\"codigoMH\": \"BF\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1096,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "BI", "{\"codigoMH\": \"BI\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1097,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BT", "{\"codigoMH\": \"BT\"}", "Bután" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1098,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CV", "{\"codigoMH\": \"CV\"}", "Cabo Verde" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1099,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KY", "{\"codigoMH\": \"KY\"}", "Caimán, Islas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1100,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KH", "{\"codigoMH\": \"KH\"}", "Camboya" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1101,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CM", "{\"codigoMH\": \"CM\"}", "Camerún" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1102,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CA", "{\"codigoMH\": \"CA\"}", "Canadá" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1103,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CF", "{\"codigoMH\": \"CF\"}", "Centroafricana, República" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1104,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TD", "{\"codigoMH\": \"TD\"}", "Chad" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1105,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CL", "{\"codigoMH\": \"CL\"}", "Chile" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1106,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CN", "{\"codigoMH\": \"CN\"}", "China" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1107,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CY", "{\"codigoMH\": \"CY\"}", "Chipre" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1108,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "VA", "{\"codigoMH\": \"VA\"}", "Ciudad del Vaticano" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1109,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CO", "{\"codigoMH\": \"CO\"}", "Colombia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1110,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KM", "{\"codigoMH\": \"KM\"}", "Comoras" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1111,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CG", "{\"codigoMH\": \"CG\"}", "Congo" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1112,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CI", "{\"codigoMH\": \"CI\"}", "Costa de Marfil" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1113,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "CR", "{\"codigoMH\": \"CR\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1114,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "HR", "{\"codigoMH\": \"HR\"}", "Croacia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1115,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CU", "{\"codigoMH\": \"CU\"}", "Cuba" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1116,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CW", "{\"codigoMH\": \"CW\"}", "Curazao" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1117,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "DK", "{\"codigoMH\": \"DK\"}", "Dinamarca" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1118,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "DM", "{\"codigoMH\": \"DM\"}", "Dominica" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1119,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "DJ", "{\"codigoMH\": \"DJ\"}", "Djiboutí" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1120,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "EC", "{\"codigoMH\": \"EC\"}", "Ecuador" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1121,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "EG", "{\"codigoMH\": \"EG\"}", "Egipto" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1122,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SV", "{\"codigoMH\": \"SV\"}", "El Salvador" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1123,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AE", "{\"codigoMH\": \"AE\"}", "Emiratos Árabes Unidos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1124,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ER", "{\"codigoMH\": \"ER\"}", "Eritrea" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1125,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SK", "{\"codigoMH\": \"SK\"}", "Eslovaquia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1126,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SI", "{\"codigoMH\": \"SI\"}", "Eslovenia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1127,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ES", "{\"codigoMH\": \"ES\"}", "España" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1128,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "US", "{\"codigoMH\": \"US\"}", "Estados Unidos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1129,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "EE", "{\"codigoMH\": \"EE\"}", "Estonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1130,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ET", "{\"codigoMH\": \"ET\"}", "Etiopía" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1131,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "FJ", "{\"codigoMH\": \"FJ\"}", "Fiji" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1132,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PH", "{\"codigoMH\": \"PH\"}", "Filipinas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1133,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "FI", "{\"codigoMH\": \"FI\"}", "Finlandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1134,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "FR", "{\"codigoMH\": \"FR\"}", "Francia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1135,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GA", "{\"codigoMH\": \"GA\"}", "Gabón" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1136,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GM", "{\"codigoMH\": \"GM\"}", "Gambia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1137,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GE", "{\"codigoMH\": \"GE\"}", "Georgia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1138,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GH", "{\"codigoMH\": \"GH\"}", "Ghana" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1139,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GI", "{\"codigoMH\": \"GI\"}", "Gibraltar" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1140,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GD", "{\"codigoMH\": \"GD\"}", "Granada" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1141,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GR", "{\"codigoMH\": \"GR\"}", "Grecia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1142,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GL", "{\"codigoMH\": \"GL\"}", "Groenlandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1143,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GP", "{\"codigoMH\": \"GP\"}", "Guadalupe" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1144,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GU", "{\"codigoMH\": \"GU\"}", "Guam" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1145,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GT", "{\"codigoMH\": \"GT\"}", "Guatemala" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1146,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GF", "{\"codigoMH\": \"GF\"}", "Guayana Francesa" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1147,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GG", "{\"codigoMH\": \"GG\"}", "Guernsey" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1148,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GN", "{\"codigoMH\": \"GN\"}", "Guinea" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1149,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GQ", "{\"codigoMH\": \"GQ\"}", "Guinea Ecuatorial" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1150,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GW", "{\"codigoMH\": \"GW\"}", "Guinea-Bissau" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1151,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GY", "{\"codigoMH\": \"GY\"}", "Guyana" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1152,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "HT", "{\"codigoMH\": \"HT\"}", "Haití" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1153,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "HN", "{\"codigoMH\": \"HN\"}", "Honduras" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1154,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "HK", "{\"codigoMH\": \"HK\"}", "Hong Kong" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1155,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "HU", "{\"codigoMH\": \"HU\"}", "Hungría" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1156,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IN", "{\"codigoMH\": \"IN\"}", "India" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1157,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ID", "{\"codigoMH\": \"ID\"}", "Indonesia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1158,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IQ", "{\"codigoMH\": \"IQ\"}", "Irak" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1159,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IE", "{\"codigoMH\": \"IE\"}", "Irlanda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1160,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BV", "{\"codigoMH\": \"BV\"}", "Isla Bouvet" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1161,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IM", "{\"codigoMH\": \"IM\"}", "Isla de Man" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1162,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NF", "{\"codigoMH\": \"NF\"}", "Isla Norfolk" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1163,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IS", "{\"codigoMH\": \"IS\"}", "Islandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1164,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CX", "{\"codigoMH\": \"CX\"}", "Islas Navidad" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1165,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CC", "{\"codigoMH\": \"CC\"}", "Islas Cocos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1166,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CK", "{\"codigoMH\": \"CK\"}", "Islas Cook" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1167,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "FO", "{\"codigoMH\": \"FO\"}", "Islas Faroe" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1168,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GS", "{\"codigoMH\": \"GS\"}", "Islas Georgias d. S.-Sandwich d. S." });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1169,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "HM", "{\"codigoMH\": \"HM\"}", "Islas Heard y McDonald" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1170,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "FK", "{\"codigoMH\": \"FK\"}", "Islas Malvinas (Falkland)" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1171,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MP", "{\"codigoMH\": \"MP\"}", "Islas Marianas del Norte" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1172,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MH", "{\"codigoMH\": \"MH\"}", "Islas Marshall" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1173,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PN", "{\"codigoMH\": \"PN\"}", "Islas Pitcairn" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1174,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TC", "{\"codigoMH\": \"TC\"}", "Islas Turcas y Caicos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1175,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "UM", "{\"codigoMH\": \"UM\"}", "Islas Ultramarinas de E.E.U.U" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1176,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "VI", "{\"codigoMH\": \"VI\"}", "Islas Vírgenes" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1177,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IL", "{\"codigoMH\": \"IL\"}", "Israel" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1178,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IT", "{\"codigoMH\": \"IT\"}", "Italia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1179,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "JM", "{\"codigoMH\": \"JM\"}", "Jamaica" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1180,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "JP", "{\"codigoMH\": \"JP\"}", "Japón" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1181,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "JE", "{\"codigoMH\": \"JE\"}", "Jersey" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1182,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "JO", "{\"codigoMH\": \"JO\"}", "Jordania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1183,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KZ", "{\"codigoMH\": \"KZ\"}", "Kazajistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1184,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KE", "{\"codigoMH\": \"KE\"}", "Kenia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1185,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KG", "{\"codigoMH\": \"KG\"}", "Kirguistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1186,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KI", "{\"codigoMH\": \"KI\"}", "Kiribati" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1187,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KW", "{\"codigoMH\": \"KW\"}", "Kuwait" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1188,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LA", "{\"codigoMH\": \"LA\"}", "Laos, República Democrática" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1189,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LS", "{\"codigoMH\": \"LS\"}", "Lesotho" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1190,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LV", "{\"codigoMH\": \"LV\"}", "Letonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1191,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LB", "{\"codigoMH\": \"LB\"}", "Líbano" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1192,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LR", "{\"codigoMH\": \"LR\"}", "Liberia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1193,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LY", "{\"codigoMH\": \"LY\"}", "Libia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1194,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LI", "{\"codigoMH\": \"LI\"}", "Liechtenstein" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1195,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LT", "{\"codigoMH\": \"LT\"}", "Lituania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1196,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LU", "{\"codigoMH\": \"LU\"}", "Luxemburgo" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1197,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MO", "{\"codigoMH\": \"MO\"}", "Macao" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1198,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MK", "{\"codigoMH\": \"MK\"}", "Macedonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1199,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MG", "{\"codigoMH\": \"MG\"}", "Madagascar" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1200,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MY", "{\"codigoMH\": \"MY\"}", "Malasia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1201,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MW", "{\"codigoMH\": \"MW\"}", "Malawi" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1202,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MV", "{\"codigoMH\": \"MV\"}", "Maldivas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1203,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ML", "{\"codigoMH\": \"ML\"}", "Malí" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1204,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MT", "{\"codigoMH\": \"MT\"}", "Malta" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1205,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MA", "{\"codigoMH\": \"MA\"}", "Marruecos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1206,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MQ", "{\"codigoMH\": \"MQ\"}", "Martinica e.a." });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1207,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MU", "{\"codigoMH\": \"MU\"}", "Mauricio" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1208,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MR", "{\"codigoMH\": \"MR\"}", "Mauritania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1209,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "YT", "{\"codigoMH\": \"YT\"}", "Mayotte" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1210,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MX", "{\"codigoMH\": \"MX\"}", "México" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1211,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "FM", "{\"codigoMH\": \"FM\"}", "Micronesia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1212,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MD", "{\"codigoMH\": \"MD\"}", "Moldavia, República de" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1213,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MC", "{\"codigoMH\": \"MC\"}", "Mónaco" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1214,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MN", "{\"codigoMH\": \"MN\"}", "Mongolia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1215,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ME", "{\"codigoMH\": \"ME\"}", "Montenegro" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1216,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MS", "{\"codigoMH\": \"MS\"}", "Montserrat" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1217,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MZ", "{\"codigoMH\": \"MZ\"}", "Mozambique" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1218,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MM", "{\"codigoMH\": \"MM\"}", "Myanmar" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1219,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NA", "{\"codigoMH\": \"NA\"}", "Namibia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1220,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NR", "{\"codigoMH\": \"NR\"}", "Nauru" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1221,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NP", "{\"codigoMH\": \"NP\"}", "Nepal" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1222,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NI", "{\"codigoMH\": \"NI\"}", "Nicaragua" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1223,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NE", "{\"codigoMH\": \"NE\"}", "Níger" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1224,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NG", "{\"codigoMH\": \"NG\"}", "Nigeria" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1225,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NU", "{\"codigoMH\": \"NU\"}", "Niue" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1226,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NO", "{\"codigoMH\": \"NO\"}", "Noruega" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1227,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NC", "{\"codigoMH\": \"NC\"}", "Nueva Caledonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1228,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NZ", "{\"codigoMH\": \"NZ\"}", "Nueva Zelanda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1229,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "OM", "{\"codigoMH\": \"OM\"}", "Omán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1230,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "NL", "{\"codigoMH\": \"NL\"}", "Países Bajos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1231,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PK", "{\"codigoMH\": \"PK\"}", "Pakistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1232,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PW", "{\"codigoMH\": \"PW\"}", "Palaos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1233,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PS", "{\"codigoMH\": \"PS\"}", "Palestina" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1234,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PA", "{\"codigoMH\": \"PA\"}", "Panamá" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1235,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PG", "{\"codigoMH\": \"PG\"}", "Papúa, Nueva Guinea" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1236,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PY", "{\"codigoMH\": \"PY\"}", "Paraguay" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1237,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PE", "{\"codigoMH\": \"PE\"}", "Perú" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1238,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PF", "{\"codigoMH\": \"PF\"}", "Polinesia Francesa" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1239,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PL", "{\"codigoMH\": \"PL\"}", "Polonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1240,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PT", "{\"codigoMH\": \"PT\"}", "Portugal" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1241,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PR", "{\"codigoMH\": \"PR\"}", "Puerto Rico" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1242,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "QA", "{\"codigoMH\": \"QA\"}", "Qatar" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1243,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "GB", "{\"codigoMH\": \"GB\"}", "Reino Unido" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1244,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KP", "{\"codigoMH\": \"KP\"}", "Rep. Democrática popular de Corea" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1245,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CZ", "{\"codigoMH\": \"CZ\"}", "República Checa" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1246,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KR", "{\"codigoMH\": \"KR\"}", "República de Corea" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1247,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CD", "{\"codigoMH\": \"CD\"}", "República Democrática del Congo" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1248,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "DO", "{\"codigoMH\": \"DO\"}", "República Dominicana" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1249,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IR", "{\"codigoMH\": \"IR\"}", "República Islámica de Irán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1250,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "RE", "{\"codigoMH\": \"RE\"}", "Reunión" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1251,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "RW", "{\"codigoMH\": \"RW\"}", "Ruanda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1252,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "RO", "{\"codigoMH\": \"RO\"}", "Rumania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1253,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "RU", "{\"codigoMH\": \"RU\"}", "Rusia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1254,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "EH", "{\"codigoMH\": \"EH\"}", "Sahara Occidental" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1255,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "BL", "{\"codigoMH\": \"BL\"}", "Saint Barthélemy" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1256,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "MF", "{\"codigoMH\": \"MF\"}", "Saint Martin (French part)" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1257,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SB", "{\"codigoMH\": \"SB\"}", "Salomón, Islas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1258,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "WS", "{\"codigoMH\": \"WS\"}", "Samoa" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1259,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "AS", "{\"codigoMH\": \"AS\"}", "Samoa Americana" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1260,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "KN", "{\"codigoMH\": \"KN\"}", "San Cristóbal y Nieves" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1261,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SM", "{\"codigoMH\": \"SM\"}", "San Marino" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1262,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "PM", "{\"codigoMH\": \"PM\"}", "San Pedro y Miquelón" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1263,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "VC", "{\"codigoMH\": \"VC\"}", "San Vicente y las Granadinas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1264,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SH", "{\"codigoMH\": \"SH\"}", "Santa Elena" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1265,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LC", "{\"codigoMH\": \"LC\"}", "Santa Lucía" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1266,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ST", "{\"codigoMH\": \"ST\"}", "Santo Tomé y Príncipe" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1267,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SN", "{\"codigoMH\": \"SN\"}", "Senegal" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1268,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "RS", "{\"codigoMH\": \"RS\"}", "Serbia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1269,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SC", "{\"codigoMH\": \"SC\"}", "Seychelles" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1270,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SL", "{\"codigoMH\": \"SL\"}", "Sierra Leona" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1271,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SG", "{\"codigoMH\": \"SG\"}", "Singapur" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1272,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SX", "{\"codigoMH\": \"SX\"}", "Sint Maarten (Dutch part)" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1273,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SY", "{\"codigoMH\": \"SY\"}", "Siria" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1274,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SO", "{\"codigoMH\": \"SO\"}", "Somalia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1275,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SS", "{\"codigoMH\": \"SS\"}", "South Sudan" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1276,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "LK", "{\"codigoMH\": \"LK\"}", "Sri Lanka" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1277,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ZA", "{\"codigoMH\": \"ZA\"}", "Sudáfrica" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1278,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SD", "{\"codigoMH\": \"SD\"}", "Sudán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1279,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SE", "{\"codigoMH\": \"SE\"}", "Suecia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1280,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "CH", "{\"codigoMH\": \"CH\"}", "Suiza" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1281,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SR", "{\"codigoMH\": \"SR\"}", "Surinám" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1282,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SJ", "{\"codigoMH\": \"SJ\"}", "Svalbard y Jan Mayen" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1283,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "SZ", "{\"codigoMH\": \"SZ\"}", "Swazilandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1284,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "TH", "{\"codigoMH\": \"TH\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1285,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TW", "{\"codigoMH\": \"TW\"}", "Taiwan, Provincia de China" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1286,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TZ", "{\"codigoMH\": \"TZ\"}", "Tanzania, República Unida de" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1287,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TJ", "{\"codigoMH\": \"TJ\"}", "Tayikistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1288,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "IO", "{\"codigoMH\": \"IO\"}", "Territorio Británico Océano Indico" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1289,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TF", "{\"codigoMH\": \"TF\"}", "Territorios Australes Franceses" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1290,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TL", "{\"codigoMH\": \"TL\"}", "Timor Oriental" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1291,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TG", "{\"codigoMH\": \"TG\"}", "Togo" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1292,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TK", "{\"codigoMH\": \"TK\"}", "Tokelau" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1293,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TO", "{\"codigoMH\": \"TO\"}", "Tonga" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1294,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TT", "{\"codigoMH\": \"TT\"}", "Trinidad y Tobago" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1295,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "TN", "{\"codigoMH\": \"TN\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1296,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "TM", "{\"codigoMH\": \"TM\"}", "Turkmenistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1297,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "TR", "{\"codigoMH\": \"TR\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1298,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "TV", "{\"codigoMH\": \"TV\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1299,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "UA", "{\"codigoMH\": \"UA\"}", "Ucrania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1300,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "UG", "{\"codigoMH\": \"UG\"}", "Uganda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1301,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "UY", "{\"codigoMH\": \"UY\"}", "Uruguay" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1302,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "UZ", "{\"codigoMH\": \"UZ\"}", "Uzbekistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1303,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "VU", "{\"codigoMH\": \"VU\"}", "Vanuatu" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1304,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "VE", "{\"codigoMH\": \"VE\"}", "Venezuela" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1305,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "VN", "{\"codigoMH\": \"VN\"}", "Vietnam" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1306,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "VG", "{\"codigoMH\": \"VG\"}", "Islas Vírgenes Británicas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1307,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "WF", "{\"codigoMH\": \"WF\"}", "Wallis y Fortuna, Islas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1308,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "YE", "{\"codigoMH\": \"YE\"}", "Yemen" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1309,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ZM", "{\"codigoMH\": \"ZM\"}", "Zambia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1310,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "ZW", "{\"codigoMH\": \"ZW\"}", "Zimbabue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1062,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9300", "{\"codigoMH\": \"9300\", \"nombreMH\": \"EL SALVADOR\"}", "El Salvador" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1063,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9303", "{\"codigoMH\": \"9303\", \"nombreMH\": \"AFGANISTÁN\"}", "Afganistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1064,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9304", "{\"codigoMH\": \"9304\", \"nombreMH\": \"ALAND\"}", "Aland" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1065,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9306", "{\"codigoMH\": \"9306\", \"nombreMH\": \"ALBANIA\"}", "Albania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1066,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9309", "{\"codigoMH\": \"9309\", \"nombreMH\": \"ALEMANIA OCCID\"}", "Alemania Occid" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1067,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9310", "{\"codigoMH\": \"9310\", \"nombreMH\": \"ALEMANIA ORIENT\"}", "Alemania Orient" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1068,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9311", "{\"codigoMH\": \"9311\", \"nombreMH\": \"ALEMANIA\"}", "Alemania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1069,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9315", "{\"codigoMH\": \"9315\", \"nombreMH\": \"ALTO VOLTA\"}", "Alto Volta" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1070,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9317", "{\"codigoMH\": \"9317\", \"nombreMH\": \"ANDORRA\"}", "Andorra" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1071,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9318", "{\"codigoMH\": \"9318\", \"nombreMH\": \"ANGOLA\"}", "Angola" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1072,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9319", "{\"codigoMH\": \"9319\", \"nombreMH\": \"ANTIG Y BARBUDA\"}", "Antig Y Barbuda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1073,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9320", "{\"codigoMH\": \"9320\", \"nombreMH\": \"ANGUILA\"}", "Anguila" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1074,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9324", "{\"codigoMH\": \"9324\", \"nombreMH\": \"ARABIA SAUDITA\"}", "Arabia Saudita" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1075,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9327", "{\"codigoMH\": \"9327\", \"nombreMH\": \"ARGELIA\"}", "Argelia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1076,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9330", "{\"codigoMH\": \"9330\", \"nombreMH\": \"ARGENTINA\"}", "Argentina" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1077,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9332", "{\"codigoMH\": \"9332\", \"nombreMH\": \"ARUBA\"}", "Aruba" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1078,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9333", "{\"codigoMH\": \"9333\", \"nombreMH\": \"AUSTRALIA\"}", "Australia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1079,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9336", "{\"codigoMH\": \"9336\", \"nombreMH\": \"AUSTRIA\"}", "Austria" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1080,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9338", "{\"codigoMH\": \"9338\", \"nombreMH\": \"AZERBAIYÁN\"}", "Azerbaiyán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1081,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9339", "{\"codigoMH\": \"9339\", \"nombreMH\": \"BANGLADESH\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1082,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9342", "{\"codigoMH\": \"9342\", \"nombreMH\": \"BAHRÉIN\"}", "Bahréin" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1083,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9345", "{\"codigoMH\": \"9345\", \"nombreMH\": \"BARBADOS\"}", "Barbados" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1084,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9348", "{\"codigoMH\": \"9348\", \"nombreMH\": \"BÉLGICA\"}", "Bélgica" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1085,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9349", "{\"codigoMH\": \"9349\", \"nombreMH\": \"BELICE\"}", "Belice" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1086,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9350", "{\"codigoMH\": \"9350\", \"nombreMH\": \"BENÍN\"}", "Benín" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1087,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9353", "{\"codigoMH\": \"9353\", \"nombreMH\": \"BIELORRUSIA\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1088,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9354", "{\"codigoMH\": \"9354\", \"nombreMH\": \"BIRMANIA\"}", "Birmania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1089,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9357", "{\"codigoMH\": \"9357\", \"nombreMH\": \"BOLIVIA\"}", "Bolivia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1090,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9359", "{\"codigoMH\": \"9359\", \"nombreMH\": \"BOSNIA Y HERZEGOVINA\"}", "Bosnia Y Herzegovina" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1091,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9360", "{\"codigoMH\": \"9360\", \"nombreMH\": \"BOTSWANA\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1092,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9363", "{\"codigoMH\": \"9363\", \"nombreMH\": \"BRASIL\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1093,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9366", "{\"codigoMH\": \"9366\", \"nombreMH\": \"BRUNÉI\"}", "Brunéi" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1094,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9369", "{\"codigoMH\": \"9369\", \"nombreMH\": \"BULGARIA\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1095,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9371", "{\"codigoMH\": \"9371\", \"nombreMH\": \"BURKINA FASO\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1096,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9372", "{\"codigoMH\": \"9372\", \"nombreMH\": \"BURUNDI\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1097,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9374", "{\"codigoMH\": \"9374\", \"nombreMH\": \"BOPHUTHATSWANA\"}", "Bophuthatswana" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1098,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9375", "{\"codigoMH\": \"9375\", \"nombreMH\": \"BUTÁN\"}", "Bután" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1099,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9376", "{\"codigoMH\": \"9376\", \"nombreMH\": \"CABINDA\"}", "Cabinda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1100,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9377", "{\"codigoMH\": \"9377\", \"nombreMH\": \"CABO VERDE\"}", "Cabo Verde" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1101,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9378", "{\"codigoMH\": \"9378\", \"nombreMH\": \"CAMBOYA\"}", "Camboya" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1102,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9381", "{\"codigoMH\": \"9381\", \"nombreMH\": \"CAMERÚN\"}", "Camerún" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1103,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9384", "{\"codigoMH\": \"9384\", \"nombreMH\": \"CANADÁ\"}", "Canadá" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1104,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9387", "{\"codigoMH\": \"9387\", \"nombreMH\": \"CEILÁN\"}", "Ceilán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1105,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9390", "{\"codigoMH\": \"9390\", \"nombreMH\": \"CTRO AFRIC REP\"}", "Ctro Afric Rep" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1106,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9393", "{\"codigoMH\": \"9393\", \"nombreMH\": \"COLOMBIA\"}", "Colombia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1107,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9394", "{\"codigoMH\": \"9394\", \"nombreMH\": \"COMORAS-ISLAS\"}", "Comoras-Islas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1108,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9396", "{\"codigoMH\": \"9396\", \"nombreMH\": \"CONGO REP DEL\"}", "Congo Rep Del" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1109,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9399", "{\"codigoMH\": \"9399\", \"nombreMH\": \"CONGO REP DEMOC\"}", "Congo Rep Democ" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1110,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9402", "{\"codigoMH\": \"9402\", \"nombreMH\": \"COREA NORTE\"}", "Corea Norte" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1111,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9405", "{\"codigoMH\": \"9405\", \"nombreMH\": \"COREA SUR\"}", "Corea Sur" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1112,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9408", "{\"codigoMH\": \"9408\", \"nombreMH\": \"COSTA DE MARFIL\"}", "Costa De Marfil" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1113,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9411", "{\"codigoMH\": \"9411\", \"nombreMH\": \"COSTA RICA\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1114,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9414", "{\"codigoMH\": \"9414\", \"nombreMH\": \"CUBA\"}", "Cuba" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1115,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9415", "{\"codigoMH\": \"9415\", \"nombreMH\": \"CURAZAO\"}", "Curazao" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1116,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9417", "{\"codigoMH\": \"9417\", \"nombreMH\": \"CHAD\"}", "Chad" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1117,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9420", "{\"codigoMH\": \"9420\", \"nombreMH\": \"CHECOSLOVAQUIA\"}", "Checoslovaquia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1118,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9423", "{\"codigoMH\": \"9423\", \"nombreMH\": \"CHILE\"}", "Chile" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1119,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9426", "{\"codigoMH\": \"9426\", \"nombreMH\": \"CHINA REP POPUL\"}", "China Rep Popul" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1120,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9432", "{\"codigoMH\": \"9432\", \"nombreMH\": \"CHIPRE\"}", "Chipre" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1121,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9435", "{\"codigoMH\": \"9435\", \"nombreMH\": \"DAHOMEY\"}", "Dahomey" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1122,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9438", "{\"codigoMH\": \"9438\", \"nombreMH\": \"DINAMARCA\"}", "Dinamarca" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1123,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9439", "{\"codigoMH\": \"9439\", \"nombreMH\": \"DJIBOUTI\"}", "Djibouti" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1124,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9440", "{\"codigoMH\": \"9440\", \"nombreMH\": \"DOMINICA\"}", "Dominica" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1125,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9441", "{\"codigoMH\": \"9441\", \"nombreMH\": \"DOMINICANA REP\"}", "Dominicana Rep" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1126,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9444", "{\"codigoMH\": \"9444\", \"nombreMH\": \"ECUADOR\"}", "Ecuador" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1127,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9446", "{\"codigoMH\": \"9446\", \"nombreMH\": \"EMIRAT ARAB UNI\"}", "Emirat Arab Uni" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1128,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9447", "{\"codigoMH\": \"9447\", \"nombreMH\": \"ESPAÑA\"}", "España" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1129,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9449", "{\"codigoMH\": \"9449\", \"nombreMH\": \"ESLOVAQUIA\"}", "Eslovaquia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1130,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9450", "{\"codigoMH\": \"9450\", \"nombreMH\": \"EE UU\"}", "Ee Uu" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1131,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9451", "{\"codigoMH\": \"9451\", \"nombreMH\": \"ESLOVENIA\"}", "Eslovenia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1132,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9452", "{\"codigoMH\": \"9452\", \"nombreMH\": \"WALLIS Y FUTUNA\"}", "Wallis Y Futuna" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1133,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9453", "{\"codigoMH\": \"9453\", \"nombreMH\": \"ETIOPIA\"}", "Etiopia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1134,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9454", "{\"codigoMH\": \"9454\", \"nombreMH\": \"ERITREA\"}", "Eritrea" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1135,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9456", "{\"codigoMH\": \"9456\", \"nombreMH\": \"FIJI-ISLAS\"}", "Fiji-Islas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1136,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9457", "{\"codigoMH\": \"9457\", \"nombreMH\": \"ESTONIA\"}", "Estonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1137,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9459", "{\"codigoMH\": \"9459\", \"nombreMH\": \"FILIPINAS\"}", "Filipinas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1138,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9462", "{\"codigoMH\": \"9462\", \"nombreMH\": \"FINLANDIA\"}", "Finlandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1139,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9465", "{\"codigoMH\": \"9465\", \"nombreMH\": \"FRANCIA\"}", "Francia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1140,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9468", "{\"codigoMH\": \"9468\", \"nombreMH\": \"GABÓN\"}", "Gabón" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1141,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9471", "{\"codigoMH\": \"9471\", \"nombreMH\": \"GAMBIA\"}", "Gambia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1142,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9472", "{\"codigoMH\": \"9472\", \"nombreMH\": \"GEORGIA\"}", "Georgia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1143,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9474", "{\"codigoMH\": \"9474\", \"nombreMH\": \"GHANA\"}", "Ghana" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1144,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9477", "{\"codigoMH\": \"9477\", \"nombreMH\": \"GIBRALTAR\"}", "Gibraltar" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1145,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9480", "{\"codigoMH\": \"9480\", \"nombreMH\": \"GRECIA\"}", "Grecia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1146,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9481", "{\"codigoMH\": \"9481\", \"nombreMH\": \"GRENADA\"}", "Grenada" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1147,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9482", "{\"codigoMH\": \"9482\", \"nombreMH\": \"GROENLANDIA\"}", "Groenlandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1148,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9483", "{\"codigoMH\": \"9483\", \"nombreMH\": \"GUATEMALA\"}", "Guatemala" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1149,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9486", "{\"codigoMH\": \"9486\", \"nombreMH\": \"GUINEA\"}", "Guinea" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1150,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9487", "{\"codigoMH\": \"9487\", \"nombreMH\": \"GUYANA\"}", "Guyana" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1151,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9489", "{\"codigoMH\": \"9489\", \"nombreMH\": \"GUADALUPE\"}", "Guadalupe" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1152,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9490", "{\"codigoMH\": \"9490\", \"nombreMH\": \"GUAM\"}", "Guam" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1153,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9491", "{\"codigoMH\": \"9491\", \"nombreMH\": \"GUAYANA FRANCESA\"}", "Guayana Francesa" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1154,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9492", "{\"codigoMH\": \"9492\", \"nombreMH\": \"GUERNSEY\"}", "Guernsey" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1155,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9493", "{\"codigoMH\": \"9493\", \"nombreMH\": \"GUINEA ECUATORIAL\"}", "Guinea Ecuatorial" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1156,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9494", "{\"codigoMH\": \"9494\", \"nombreMH\": \"GUINEA-BISSAU\"}", "Guinea-Bissau" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1157,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9495", "{\"codigoMH\": \"9495\", \"nombreMH\": \"HAITÍ\"}", "Haití" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1158,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9498", "{\"codigoMH\": \"9498\", \"nombreMH\": \"HOLANDA\"}", "Holanda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1159,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9501", "{\"codigoMH\": \"9501\", \"nombreMH\": \"HONDURAS\"}", "Honduras" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1160,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9504", "{\"codigoMH\": \"9504\", \"nombreMH\": \"HONG KONG\"}", "Hong Kong" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1161,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9507", "{\"codigoMH\": \"9507\", \"nombreMH\": \"HUNGRÍA\"}", "Hungría" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1162,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9510", "{\"codigoMH\": \"9510\", \"nombreMH\": \"INDIA\"}", "India" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1163,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9513", "{\"codigoMH\": \"9513\", \"nombreMH\": \"INDONESIA\"}", "Indonesia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1164,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9514", "{\"codigoMH\": \"9514\", \"nombreMH\": \"INGLATERRA Y GALES\"}", "Inglaterra Y Gales" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1165,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9516", "{\"codigoMH\": \"9516\", \"nombreMH\": \"IRAK\"}", "Irak" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1166,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9519", "{\"codigoMH\": \"9519\", \"nombreMH\": \"IRÁN\"}", "Irán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1167,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9521", "{\"codigoMH\": \"9521\", \"nombreMH\": \"ISLA DE MAN\"}", "Isla De Man" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1168,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9522", "{\"codigoMH\": \"9522\", \"nombreMH\": \"IRLANDA\"}", "Irlanda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1169,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9523", "{\"codigoMH\": \"9523\", \"nombreMH\": \"ISLA DE NAVIDAD\"}", "Isla De Navidad" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1170,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9524", "{\"codigoMH\": \"9524\", \"nombreMH\": \"ISLA DE COCOS\"}", "Isla De Cocos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1171,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9525", "{\"codigoMH\": \"9525\", \"nombreMH\": \"ISLANDIA\"}", "Islandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1172,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9526", "{\"codigoMH\": \"9526\", \"nombreMH\": \"ISLAS SALOMÓN\"}", "Islas Salomón" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1173,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9527", "{\"codigoMH\": \"9527\", \"nombreMH\": \"ISLAS COOK\"}", "Islas Cook" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1174,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9528", "{\"codigoMH\": \"9528\", \"nombreMH\": \"ISRAEL\"}", "Israel" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1175,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9529", "{\"codigoMH\": \"9529\", \"nombreMH\": \"ISLAS FEROE\"}", "Islas Feroe" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1176,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9530", "{\"codigoMH\": \"9530\", \"nombreMH\": \"ISLAS AZORES\"}", "Islas Azores" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1177,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9531", "{\"codigoMH\": \"9531\", \"nombreMH\": \"ITALIA\"}", "Italia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1178,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9532", "{\"codigoMH\": \"9532\", \"nombreMH\": \"ISLA QESHM\"}", "Isla Qeshm" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1179,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9533", "{\"codigoMH\": \"9533\", \"nombreMH\": \"ISLAS MALVINAS\"}", "Islas Malvinas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1180,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9534", "{\"codigoMH\": \"9534\", \"nombreMH\": \"JAMAICA\"}", "Jamaica" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1181,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9535", "{\"codigoMH\": \"9535\", \"nombreMH\": \"ISLAS MARIANAS DEL NORTE\"}", "Islas Marianas Del Norte" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1182,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9536", "{\"codigoMH\": \"9536\", \"nombreMH\": \"ISLAS MARSHALL\"}", "Islas Marshall" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1183,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9537", "{\"codigoMH\": \"9537\", \"nombreMH\": \"JAPÓN\"}", "Japón" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1184,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9538", "{\"codigoMH\": \"9538\", \"nombreMH\": \"ISLAS PITCAIM\"}", "Islas Pitcaim" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1185,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9539", "{\"codigoMH\": \"9539\", \"nombreMH\": \"ISLAS TURCAS Y CAICOS\"}", "Islas Turcas Y Caicos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1186,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9540", "{\"codigoMH\": \"9540\", \"nombreMH\": \"JORDANIA\"}", "Jordania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1187,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9541", "{\"codigoMH\": \"9541\", \"nombreMH\": \"KASAKISTAN\"}", "Kasakistan" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1188,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9542", "{\"codigoMH\": \"9542\", \"nombreMH\": \"ISLAS ULTRAMARINAS DE EE UU\"}", "Islas Ultramarinas De Ee Uu" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1189,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9543", "{\"codigoMH\": \"9543\", \"nombreMH\": \"KENIA\"}", "Kenia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1190,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9544", "{\"codigoMH\": \"9544\", \"nombreMH\": \"KIRIBATI\"}", "Kiribati" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1191,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9545", "{\"codigoMH\": \"9545\", \"nombreMH\": \"ISLAS VÍRGENES ESTADOUNIDENSES\"}", "Islas Vírgenes Estadounidenses" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1192,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9546", "{\"codigoMH\": \"9546\", \"nombreMH\": \"KUWAIT\"}", "Kuwait" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1193,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9547", "{\"codigoMH\": \"9547\", \"nombreMH\": \"JERSEY\"}", "Jersey" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1194,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9548", "{\"codigoMH\": \"9548\", \"nombreMH\": \"KIRGUISTÁN\"}", "Kirguistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1195,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9549", "{\"codigoMH\": \"9549\", \"nombreMH\": \"LAOS\"}", "Laos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1196,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9551", "{\"codigoMH\": \"9551\", \"nombreMH\": \"LETONIA\"}", "Letonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1197,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9552", "{\"codigoMH\": \"9552\", \"nombreMH\": \"LESOTHO\"}", "Lesotho" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1198,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9555", "{\"codigoMH\": \"9555\", \"nombreMH\": \"LÍBANO\"}", "Líbano" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1199,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9558", "{\"codigoMH\": \"9558\", \"nombreMH\": \"LIBERIA\"}", "Liberia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1200,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9561", "{\"codigoMH\": \"9561\", \"nombreMH\": \"LIBIA\"}", "Libia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1201,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9564", "{\"codigoMH\": \"9564\", \"nombreMH\": \"LIECHTENSTEIN\"}", "Liechtenstein" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1202,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9565", "{\"codigoMH\": \"9565\", \"nombreMH\": \"LITUANIA\"}", "Lituania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1203,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9567", "{\"codigoMH\": \"9567\", \"nombreMH\": \"LUXEMBURGO\"}", "Luxemburgo" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1204,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9568", "{\"codigoMH\": \"9568\", \"nombreMH\": \"MACAO\"}", "Macao" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1205,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9570", "{\"codigoMH\": \"9570\", \"nombreMH\": \"MADAGASCAR\"}", "Madagascar" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1206,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9571", "{\"codigoMH\": \"9571\", \"nombreMH\": \"MACEDONIA\"}", "Macedonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1207,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9573", "{\"codigoMH\": \"9573\", \"nombreMH\": \"MALASIA\"}", "Malasia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1208,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9574", "{\"codigoMH\": \"9574\", \"nombreMH\": \"MALI\"}", "Mali" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1209,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9576", "{\"codigoMH\": \"9576\", \"nombreMH\": \"MALAWI\"}", "Malawi" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1210,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9577", "{\"codigoMH\": \"9577\", \"nombreMH\": \"MALDIVAS\"}", "Maldivas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1211,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9579", "{\"codigoMH\": \"9579\", \"nombreMH\": \"MALI\"}", "Mali" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1212,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9582", "{\"codigoMH\": \"9582\", \"nombreMH\": \"MALTA\"}", "Malta" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1213,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9585", "{\"codigoMH\": \"9585\", \"nombreMH\": \"MARRUECOS\"}", "Marruecos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1214,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9591", "{\"codigoMH\": \"9591\", \"nombreMH\": \"MASCATE Y OMÁN\"}", "Mascate Y Omán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1215,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9594", "{\"codigoMH\": \"9594\", \"nombreMH\": \"MAURICIO\"}", "Mauricio" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1216,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9597", "{\"codigoMH\": \"9597\", \"nombreMH\": \"MAURITANIA\"}", "Mauritania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1217,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9598", "{\"codigoMH\": \"9598\", \"nombreMH\": \"MAYOTTE\"}", "Mayotte" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1218,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9600", "{\"codigoMH\": \"9600\", \"nombreMH\": \"MÉXICO\"}", "México" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1219,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9601", "{\"codigoMH\": \"9601\", \"nombreMH\": \"MICRONESIA\"}", "Micronesia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1220,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9602", "{\"codigoMH\": \"9602\", \"nombreMH\": \"MOLDAVIA\"}", "Moldavia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1221,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9603", "{\"codigoMH\": \"9603\", \"nombreMH\": \"MÓNACO\"}", "Mónaco" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1222,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9606", "{\"codigoMH\": \"9606\", \"nombreMH\": \"MONGOLIA\"}", "Mongolia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1223,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9607", "{\"codigoMH\": \"9607\", \"nombreMH\": \"MONTENEGRO\"}", "Montenegro" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1224,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9608", "{\"codigoMH\": \"9608\", \"nombreMH\": \"MONSERRAT\"}", "Monserrat" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1225,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9609", "{\"codigoMH\": \"9609\", \"nombreMH\": \"MOZAMBIQUE\"}", "Mozambique" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1226,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9610", "{\"codigoMH\": \"9610\", \"nombreMH\": \"NAMIBIA\"}", "Namibia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1227,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9611", "{\"codigoMH\": \"9611\", \"nombreMH\": \"NAURU\"}", "Nauru" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1228,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9612", "{\"codigoMH\": \"9612\", \"nombreMH\": \"NEPAL\"}", "Nepal" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1229,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9615", "{\"codigoMH\": \"9615\", \"nombreMH\": \"NICARAGUA\"}", "Nicaragua" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1230,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9618", "{\"codigoMH\": \"9618\", \"nombreMH\": \"NÍGER\"}", "Níger" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1231,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9621", "{\"codigoMH\": \"9621\", \"nombreMH\": \"NIGERIA\"}", "Nigeria" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1232,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9622", "{\"codigoMH\": \"9622\", \"nombreMH\": \"NIUE\"}", "Niue" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1233,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9623", "{\"codigoMH\": \"9623\", \"nombreMH\": \"NORFOLK\"}", "Norfolk" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1234,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9624", "{\"codigoMH\": \"9624\", \"nombreMH\": \"NORUEGA\"}", "Noruega" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1235,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9627", "{\"codigoMH\": \"9627\", \"nombreMH\": \"NVA CALEDONIA\"}", "Nva Caledonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1236,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9633", "{\"codigoMH\": \"9633\", \"nombreMH\": \"NVA ZELANDIA\"}", "Nva Zelandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1237,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9636", "{\"codigoMH\": \"9636\", \"nombreMH\": \"NUEVAS HEBRIDAS\"}", "Nuevas Hebridas" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1238,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9638", "{\"codigoMH\": \"9638\", \"nombreMH\": \"PAPUA NV GUINEA\"}", "Papua Nv Guinea" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1239,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9639", "{\"codigoMH\": \"9639\", \"nombreMH\": \"PAKISTÁN\"}", "Pakistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1240,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9640", "{\"codigoMH\": \"9640\", \"nombreMH\": \"PALESTINA\"}", "Palestina" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1241,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9641", "{\"codigoMH\": \"9641\", \"nombreMH\": \"CROACIA\"}", "Croacia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1242,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9642", "{\"codigoMH\": \"9642\", \"nombreMH\": \"PANAMÁ\"}", "Panamá" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1243,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9643", "{\"codigoMH\": \"9643\", \"nombreMH\": \"PALAOS\"}", "Palaos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1244,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9645", "{\"codigoMH\": \"9645\", \"nombreMH\": \"PARAGUAY\"}", "Paraguay" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1245,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9648", "{\"codigoMH\": \"9648\", \"nombreMH\": \"PERÚ\"}", "Perú" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1246,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9651", "{\"codigoMH\": \"9651\", \"nombreMH\": \"POLONIA\"}", "Polonia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1247,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9652", "{\"codigoMH\": \"9652\", \"nombreMH\": \"POLINESIA FRANCESA\"}", "Polinesia Francesa" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1248,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9654", "{\"codigoMH\": \"9654\", \"nombreMH\": \"PORTUGAL\"}", "Portugal" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1249,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9660", "{\"codigoMH\": \"9660\", \"nombreMH\": \"QATAR\"}", "Qatar" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1250,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9663", "{\"codigoMH\": \"9663\", \"nombreMH\": \"EL REINO UNIDO\"}", "El Reino Unido" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1251,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9664", "{\"codigoMH\": \"9664\", \"nombreMH\": \"REPUBLICA CHECA\"}", "Republica Checa" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1252,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9666", "{\"codigoMH\": \"9666\", \"nombreMH\": \"EGIPTO\"}", "Egipto" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1253,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9667", "{\"codigoMH\": \"9667\", \"nombreMH\": \"REUNIÓN\"}", "Reunión" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1254,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9669", "{\"codigoMH\": \"9669\", \"nombreMH\": \"RODESIA\"}", "Rodesia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1255,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9672", "{\"codigoMH\": \"9672\", \"nombreMH\": \"RUANDA\"}", "Ruanda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1256,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9673", "{\"codigoMH\": \"9673\", \"nombreMH\": \"REPUBLICA DE ARMENIA\"}", "Republica De Armenia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1257,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9675", "{\"codigoMH\": \"9675\", \"nombreMH\": \"RUMANIA\"}", "Rumania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1258,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9676", "{\"codigoMH\": \"9676\", \"nombreMH\": \"SAHARA OCCIDENTAL\"}", "Sahara Occidental" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1259,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9677", "{\"codigoMH\": \"9677\", \"nombreMH\": \"SAN MARINO\"}", "San Marino" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1260,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9678", "{\"codigoMH\": \"9678\", \"nombreMH\": \"SAMOA OCCID\"}", "Samoa Occid" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1261,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9679", "{\"codigoMH\": \"9679\", \"nombreMH\": \"SAINT KITTS AND NEVIS\"}", "Saint Kitts And Nevis" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1262,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9680", "{\"codigoMH\": \"9680\", \"nombreMH\": \"SANTA LUCIA\"}", "Santa Lucia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1263,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9681", "{\"codigoMH\": \"9681\", \"nombreMH\": \"SENEGAL\"}", "Senegal" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1264,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9682", "{\"codigoMH\": \"9682\", \"nombreMH\": \"SAOTOME Y PRINC\"}", "Saotome Y Princ" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1265,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9683", "{\"codigoMH\": \"9683\", \"nombreMH\": \"SN VIC Y GRENAD\"}", "Sn Vic Y Grenad" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1266,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9684", "{\"codigoMH\": \"9684\", \"nombreMH\": \"SIERRA LEONA\"}", "Sierra Leona" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1267,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9685", "{\"codigoMH\": \"9685\", \"nombreMH\": \"SAMOA AMERICANA\"}", "Samoa Americana" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1268,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9686", "{\"codigoMH\": \"9686\", \"nombreMH\": \"SAN PEDRO Y MIQUELÓN\"}", "San Pedro Y Miquelón" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1269,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9687", "{\"codigoMH\": \"9687\", \"nombreMH\": \"SINGAPUR\"}", "Singapur" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1270,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9688", "{\"codigoMH\": \"9688\", \"nombreMH\": \"SANTA ELENA\"}", "Santa Elena" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1271,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9689", "{\"codigoMH\": \"9689\", \"nombreMH\": \"SERBIA\"}", "Serbia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1272,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9690", "{\"codigoMH\": \"9690\", \"nombreMH\": \"SIRIA\"}", "Siria" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1273,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9691", "{\"codigoMH\": \"9691\", \"nombreMH\": \"SEYCHELLES\"}", "Seychelles" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1274,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9692", "{\"codigoMH\": \"9692\", \"nombreMH\": \"SVALBARD Y JAN MAYEN\"}", "Svalbard Y Jan Mayen" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1275,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9693", "{\"codigoMH\": \"9693\", \"nombreMH\": \"SOMALIA\"}", "Somalia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1276,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9696", "{\"codigoMH\": \"9696\", \"nombreMH\": \"SUDÁFRICA REP\"}", "Sudáfrica Rep" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1277,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9699", "{\"codigoMH\": \"9699\", \"nombreMH\": \"SUDAN\"}", "Sudan" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1278,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9702", "{\"codigoMH\": \"9702\", \"nombreMH\": \"SUECIA\"}", "Suecia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1279,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9705", "{\"codigoMH\": \"9705\", \"nombreMH\": \"SUIZA\"}", "Suiza" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1280,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9706", "{\"codigoMH\": \"9706\", \"nombreMH\": \"SURINAM\"}", "Surinam" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1281,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9707", "{\"codigoMH\": \"9707\", \"nombreMH\": \"SRI LANKA\"}", "Sri Lanka" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1282,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9708", "{\"codigoMH\": \"9708\", \"nombreMH\": \"SUECILANDIA\"}", "Suecilandia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1283,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9709", "{\"codigoMH\": \"9709\", \"nombreMH\": \"TAYIKISTÁN\"}", "Tayikistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1284,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9711", "{\"codigoMH\": \"9711\", \"nombreMH\": \"TAILANDIA\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1285,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9712", "{\"codigoMH\": \"9712\", \"nombreMH\": \"TERRITORIO BRITÁNICO DEL OCÉANO INDICO\"}", "Territorio Británico Del Océano Indico" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1286,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9713", "{\"codigoMH\": \"9713\", \"nombreMH\": \"TERRITORIOS AUSTRALES FRANCESES\"}", "Territorios Australes Franceses" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1287,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9714", "{\"codigoMH\": \"9714\", \"nombreMH\": \"TANZANIA\"}", "Tanzania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1288,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9715", "{\"codigoMH\": \"9715\", \"nombreMH\": \"TERRITORIOS PALESTINOS\"}", "Territorios Palestinos" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1289,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9716", "{\"codigoMH\": \"9716\", \"nombreMH\": \"TIMOR ORIENTAL\"}", "Timor Oriental" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1290,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9717", "{\"codigoMH\": \"9717\", \"nombreMH\": \"TOGO\"}", "Togo" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1291,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9718", "{\"codigoMH\": \"9718\", \"nombreMH\": \"TOKELAU\"}", "Tokelau" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1292,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9719", "{\"codigoMH\": \"9719\", \"nombreMH\": \"TURKMENISTÁN\"}", "Turkmenistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1293,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9720", "{\"codigoMH\": \"9720\", \"nombreMH\": \"TRINIDAD TOBAGO\"}", "Trinidad Tobago" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1294,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9722", "{\"codigoMH\": \"9722\", \"nombreMH\": \"TONGA\"}", "Tonga" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1295,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9723", "{\"codigoMH\": \"9723\", \"nombreMH\": \"TÚNEZ\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1296,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9725", "{\"codigoMH\": \"9725\", \"nombreMH\": \"TRANSKEI\"}", "Transkei" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1297,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9726", "{\"codigoMH\": \"9726\", \"nombreMH\": \"TURQUÍA\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1298,
                columns: new[] { "Codigo", "MetadataJson" },
                values: new object[] { "9727", "{\"codigoMH\": \"9727\", \"nombreMH\": \"TUVALU\"}" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1299,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9729", "{\"codigoMH\": \"9729\", \"nombreMH\": \"UGANDA\"}", "Uganda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1300,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9732", "{\"codigoMH\": \"9732\", \"nombreMH\": \"URSS\"}", "Urss" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1301,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9733", "{\"codigoMH\": \"9733\", \"nombreMH\": \"RUSIA\"}", "Rusia" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1302,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9735", "{\"codigoMH\": \"9735\", \"nombreMH\": \"URUGUAY\"}", "Uruguay" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1303,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9736", "{\"codigoMH\": \"9736\", \"nombreMH\": \"UCRANIA\"}", "Ucrania" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1304,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9737", "{\"codigoMH\": \"9737\", \"nombreMH\": \"UZBEKISTÁN\"}", "Uzbekistán" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1305,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9738", "{\"codigoMH\": \"9738\", \"nombreMH\": \"VATICANO\"}", "Vaticano" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1306,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9739", "{\"codigoMH\": \"9739\", \"nombreMH\": \"VANUATU\"}", "Vanuatu" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1307,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9740", "{\"codigoMH\": \"9740\", \"nombreMH\": \"VENDA\"}", "Venda" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1308,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9741", "{\"codigoMH\": \"9741\", \"nombreMH\": \"VENEZUELA\"}", "Venezuela" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1309,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9744", "{\"codigoMH\": \"9744\", \"nombreMH\": \"VIETNAM NORTE\"}", "Vietnam Norte" });

            migrationBuilder.UpdateData(
                table: "Core_CatalogoItems",
                keyColumn: "Id",
                keyValue: 1310,
                columns: new[] { "Codigo", "MetadataJson", "Valor" },
                values: new object[] { "9746", "{\"codigoMH\": \"9746\", \"nombreMH\": \"VIETNAM\"}", "Vietnam" });

            migrationBuilder.InsertData(
                table: "Core_CatalogoItems",
                columns: new[] { "Id", "Activo", "CatalogoId", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EsSistema", "MetadataJson", "Orden", "ParentCodigo", "UpdatedAt", "UpdatedBy", "Valor" },
                values: new object[,]
                {
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
                    { 1336, true, 22, "9999", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\": \"9999\", \"nombreMH\": \"No definido en migración\"}", 275, null, null, null, "No definido en migración" }
                });
        }
    }
}
