using NeoSTP.Domain.Core.Catalogos;

namespace NeoSTP.Infrastructure.Persistence.Seed;

/// <summary>
/// Sprint 13.7 — Catálogos MH oficiales basados en el Manual de Estructuras CAT v1.4.
///
/// Convención: Codigo = codigoMH. Cada ítem trae además metadata {"codigoMH":...} para
/// facilitar consultas indexadas por código MH sin tener que parsear el campo Codigo.
///
/// Cubre:
///   - CAT-006 Retención IVA MH       (catId 26)
///   - CAT-014 Unidad de Medida       (catId 15, reemplaza seed Sprint 1)
///   - CAT-018 Plazo                  (catId 27)
///   - CAT-020 País — full legacy v1.4 (catId 22, reemplaza seed Sprint 13.5; 275 países)
///   - CAT-021 Otros Documentos Asoc. (catId 28)
///   - CAT-022 Tipo Doc. Identidad    (catId 7,  reemplaza seed Sprint 1)
///   - CAT-023 Tipo Doc. Contingencia (catId 29)
///   - CAT-024 Motivo Invalidación    (catId 23, reemplaza seed Sprint 13.5; textos oficiales)
///   - CAT-025 Título Remisión        (catId 30)
///   - CAT-026 Tipo Donación          (catId 31)
///   - CAT-027 Recinto Fiscal         (catId 32)
///   - CAT-029 Tipo Persona           (catId 33)
///   - CAT-030 Transporte             (catId 34)
///   - CAT-031 INCOTERMS              (catId 35)
///   - CAT-032 Domicilio Fiscal       (catId 36)
/// </summary>
internal static partial class SeedData
{
    private static void AppendCatalogosMhOficiales(List<CatalogoItem> items, ref int id)
    {

// ----- CAT-006 RETENCION_IVA (catId=26) -----
items.Add(Item(id++, 26, "22", "Retención IVA 1%", 1, metadata: "{\"codigoMH\": \"22\"}"));
items.Add(Item(id++, 26, "C4", "Retención IVA 13%", 2, metadata: "{\"codigoMH\": \"C4\"}"));
items.Add(Item(id++, 26, "C9", "Otras retenciones IVA casos especiales", 3, metadata: "{\"codigoMH\": \"C9\"}"));

// ----- CAT-014 UNIDAD_MEDIDA (reemplaza) (catId=15) -----
items.Add(Item(id++, 15, "01", "Metro", 1, metadata: "{\"codigoMH\": \"01\"}"));
items.Add(Item(id++, 15, "02", "Yarda", 2, metadata: "{\"codigoMH\": \"02\"}"));
items.Add(Item(id++, 15, "03", "Vara", 3, metadata: "{\"codigoMH\": \"03\"}"));
items.Add(Item(id++, 15, "04", "Pie", 4, metadata: "{\"codigoMH\": \"04\"}"));
items.Add(Item(id++, 15, "05", "Pulgada", 5, metadata: "{\"codigoMH\": \"05\"}"));
items.Add(Item(id++, 15, "06", "Milímetro", 6, metadata: "{\"codigoMH\": \"06\"}"));
items.Add(Item(id++, 15, "08", "Milla cuadrada", 7, metadata: "{\"codigoMH\": \"08\"}"));
items.Add(Item(id++, 15, "09", "Kilómetro cuadrado", 8, metadata: "{\"codigoMH\": \"09\"}"));
items.Add(Item(id++, 15, "10", "Hectárea", 9, metadata: "{\"codigoMH\": \"10\"}"));
items.Add(Item(id++, 15, "11", "Manzana", 10, metadata: "{\"codigoMH\": \"11\"}"));
items.Add(Item(id++, 15, "12", "Acre", 11, metadata: "{\"codigoMH\": \"12\"}"));
items.Add(Item(id++, 15, "13", "Metro cuadrado", 12, metadata: "{\"codigoMH\": \"13\"}"));
items.Add(Item(id++, 15, "14", "Yarda cuadrada", 13, metadata: "{\"codigoMH\": \"14\"}"));
items.Add(Item(id++, 15, "15", "Vara cuadrada", 14, metadata: "{\"codigoMH\": \"15\"}"));
items.Add(Item(id++, 15, "16", "Pie cuadrado", 15, metadata: "{\"codigoMH\": \"16\"}"));
items.Add(Item(id++, 15, "17", "Pulgada cuadrada", 16, metadata: "{\"codigoMH\": \"17\"}"));
items.Add(Item(id++, 15, "18", "Metro cúbico", 17, metadata: "{\"codigoMH\": \"18\"}"));
items.Add(Item(id++, 15, "19", "Yarda cúbica", 18, metadata: "{\"codigoMH\": \"19\"}"));
items.Add(Item(id++, 15, "20", "Barril", 19, metadata: "{\"codigoMH\": \"20\"}"));
items.Add(Item(id++, 15, "21", "Pie cúbico", 20, metadata: "{\"codigoMH\": \"21\"}"));
items.Add(Item(id++, 15, "22", "Galón", 21, metadata: "{\"codigoMH\": \"22\"}"));
items.Add(Item(id++, 15, "23", "Litro", 22, metadata: "{\"codigoMH\": \"23\"}"));
items.Add(Item(id++, 15, "24", "Botella", 23, metadata: "{\"codigoMH\": \"24\"}"));
items.Add(Item(id++, 15, "25", "Pulgada cúbica", 24, metadata: "{\"codigoMH\": \"25\"}"));
items.Add(Item(id++, 15, "26", "Mililitro", 25, metadata: "{\"codigoMH\": \"26\"}"));
items.Add(Item(id++, 15, "27", "Onza fluida", 26, metadata: "{\"codigoMH\": \"27\"}"));
items.Add(Item(id++, 15, "29", "Tonelada métrica", 27, metadata: "{\"codigoMH\": \"29\"}"));
items.Add(Item(id++, 15, "30", "Tonelada", 28, metadata: "{\"codigoMH\": \"30\"}"));
items.Add(Item(id++, 15, "31", "Quintal métrico", 29, metadata: "{\"codigoMH\": \"31\"}"));
items.Add(Item(id++, 15, "32", "Quintal", 30, metadata: "{\"codigoMH\": \"32\"}"));
items.Add(Item(id++, 15, "33", "Arroba", 31, metadata: "{\"codigoMH\": \"33\"}"));
items.Add(Item(id++, 15, "34", "Kilogramo", 32, metadata: "{\"codigoMH\": \"34\"}"));
items.Add(Item(id++, 15, "35", "Libra troy", 33, metadata: "{\"codigoMH\": \"35\"}"));
items.Add(Item(id++, 15, "36", "Libra", 34, metadata: "{\"codigoMH\": \"36\"}"));
items.Add(Item(id++, 15, "37", "Onza troy", 35, metadata: "{\"codigoMH\": \"37\"}"));
items.Add(Item(id++, 15, "38", "Onza", 36, metadata: "{\"codigoMH\": \"38\"}"));
items.Add(Item(id++, 15, "39", "Gramo", 37, metadata: "{\"codigoMH\": \"39\"}"));
items.Add(Item(id++, 15, "40", "Miligramo", 38, metadata: "{\"codigoMH\": \"40\"}"));
items.Add(Item(id++, 15, "42", "Megawatt", 39, metadata: "{\"codigoMH\": \"42\"}"));
items.Add(Item(id++, 15, "43", "Kilowatt", 40, metadata: "{\"codigoMH\": \"43\"}"));
items.Add(Item(id++, 15, "44", "Watt", 41, metadata: "{\"codigoMH\": \"44\"}"));
items.Add(Item(id++, 15, "45", "Megavoltio-amperio", 42, metadata: "{\"codigoMH\": \"45\"}"));
items.Add(Item(id++, 15, "46", "Kilovoltio-amperio", 43, metadata: "{\"codigoMH\": \"46\"}"));
items.Add(Item(id++, 15, "47", "Voltio-amperio", 44, metadata: "{\"codigoMH\": \"47\"}"));
items.Add(Item(id++, 15, "49", "Gigawatt-hora", 45, metadata: "{\"codigoMH\": \"49\"}"));
items.Add(Item(id++, 15, "50", "Megawatt-hora", 46, metadata: "{\"codigoMH\": \"50\"}"));
items.Add(Item(id++, 15, "51", "Kilowatt-hora", 47, metadata: "{\"codigoMH\": \"51\"}"));
items.Add(Item(id++, 15, "52", "Watt-hora", 48, metadata: "{\"codigoMH\": \"52\"}"));
items.Add(Item(id++, 15, "53", "Kilovoltio", 49, metadata: "{\"codigoMH\": \"53\"}"));
items.Add(Item(id++, 15, "54", "Voltio", 50, metadata: "{\"codigoMH\": \"54\"}"));
items.Add(Item(id++, 15, "55", "Millar", 51, metadata: "{\"codigoMH\": \"55\"}"));
items.Add(Item(id++, 15, "56", "Medio millar", 52, metadata: "{\"codigoMH\": \"56\"}"));
items.Add(Item(id++, 15, "57", "Ciento", 53, metadata: "{\"codigoMH\": \"57\"}"));
items.Add(Item(id++, 15, "58", "Docena", 54, metadata: "{\"codigoMH\": \"58\"}"));
items.Add(Item(id++, 15, "59", "Unidad", 55, metadata: "{\"codigoMH\": \"59\"}"));
items.Add(Item(id++, 15, "99", "Otra", 56, metadata: "{\"codigoMH\": \"99\"}"));

// ----- CAT-018 PLAZO (catId=27) -----
items.Add(Item(id++, 27, "01", "Días", 1, metadata: "{\"codigoMH\": \"01\"}"));
items.Add(Item(id++, 27, "02", "Meses", 2, metadata: "{\"codigoMH\": \"02\"}"));
items.Add(Item(id++, 27, "03", "Años", 3, metadata: "{\"codigoMH\": \"03\"}"));

// ----- CAT-020 PAIS (reemplaza) (catId=22) -----

items.Add(Item(id++, 22, "AF", "Afganistán", 1, metadata: "{\"codigoMH\": \"AF\"}"));
items.Add(Item(id++, 22, "AX", "Aland", 2, metadata: "{\"codigoMH\": \"AX\"}"));
items.Add(Item(id++, 22, "AL", "Albania", 3, metadata: "{\"codigoMH\": \"AL\"}"));
items.Add(Item(id++, 22, "DE", "Alemania", 4, metadata: "{\"codigoMH\": \"DE\"}"));
items.Add(Item(id++, 22, "AD", "Andorra", 5, metadata: "{\"codigoMH\": \"AD\"}"));
items.Add(Item(id++, 22, "AO", "Angola", 6, metadata: "{\"codigoMH\": \"AO\"}"));
items.Add(Item(id++, 22, "AI", "Anguila", 7, metadata: "{\"codigoMH\": \"AI\"}"));
items.Add(Item(id++, 22, "AQ", "Antártica", 8, metadata: "{\"codigoMH\": \"AQ\"}"));
items.Add(Item(id++, 22, "AG", "Antigua y Barbuda", 9, metadata: "{\"codigoMH\": \"AG\"}"));
items.Add(Item(id++, 22, "AW", "Aruba", 10, metadata: "{\"codigoMH\": \"AW\"}"));
items.Add(Item(id++, 22, "SA", "Arabia Saudita", 11, metadata: "{\"codigoMH\": \"SA\"}"));
items.Add(Item(id++, 22, "DZ", "Argelia", 12, metadata: "{\"codigoMH\": \"DZ\"}"));
items.Add(Item(id++, 22, "AR", "Argentina", 13, metadata: "{\"codigoMH\": \"AR\"}"));
items.Add(Item(id++, 22, "AM", "Armenia", 14, metadata: "{\"codigoMH\": \"AM\"}"));
items.Add(Item(id++, 22, "AU", "Australia", 15, metadata: "{\"codigoMH\": \"AU\"}"));
items.Add(Item(id++, 22, "AT", "Austria", 16, metadata: "{\"codigoMH\": \"AT\"}"));
items.Add(Item(id++, 22, "AZ", "Azerbaiyán", 17, metadata: "{\"codigoMH\": \"AZ\"}"));
items.Add(Item(id++, 22, "BS", "Bahamas", 18, metadata: "{\"codigoMH\": \"BS\"}"));
items.Add(Item(id++, 22, "BH", "Bahrein", 19, metadata: "{\"codigoMH\": \"BH\"}"));
items.Add(Item(id++, 22, "BD", "Bangladesh", 20, metadata: "{\"codigoMH\": \"BD\"}"));
items.Add(Item(id++, 22, "BB", "Barbados", 21, metadata: "{\"codigoMH\": \"BB\"}"));
items.Add(Item(id++, 22, "BE", "Bélgica", 22, metadata: "{\"codigoMH\": \"BE\"}"));
items.Add(Item(id++, 22, "BZ", "Belice", 23, metadata: "{\"codigoMH\": \"BZ\"}"));
items.Add(Item(id++, 22, "BJ", "Benin", 24, metadata: "{\"codigoMH\": \"BJ\"}"));
items.Add(Item(id++, 22, "BM", "Bermudas", 25, metadata: "{\"codigoMH\": \"BM\"}"));
items.Add(Item(id++, 22, "BY", "Bielorrusia", 26, metadata: "{\"codigoMH\": \"BY\"}"));
items.Add(Item(id++, 22, "BO", "Bolivia", 27, metadata: "{\"codigoMH\": \"BO\"}"));
items.Add(Item(id++, 22, "BQ", "Bonaire, Sint Eustatius and Saba", 28, metadata: "{\"codigoMH\": \"BQ\"}"));
items.Add(Item(id++, 22, "BA", "Bosnia-Herzegovina", 29, metadata: "{\"codigoMH\": \"BA\"}"));
items.Add(Item(id++, 22, "BW", "Botswana", 30, metadata: "{\"codigoMH\": \"BW\"}"));
items.Add(Item(id++, 22, "BR", "Brasil", 31, metadata: "{\"codigoMH\": \"BR\"}"));
items.Add(Item(id++, 22, "BN", "Brunei", 32, metadata: "{\"codigoMH\": \"BN\"}"));
items.Add(Item(id++, 22, "BG", "Bulgaria", 33, metadata: "{\"codigoMH\": \"BG\"}"));
items.Add(Item(id++, 22, "BF", "Burkina Faso", 34, metadata: "{\"codigoMH\": \"BF\"}"));
items.Add(Item(id++, 22, "BI", "Burundi", 35, metadata: "{\"codigoMH\": \"BI\"}"));
items.Add(Item(id++, 22, "BT", "Bután", 36, metadata: "{\"codigoMH\": \"BT\"}"));
items.Add(Item(id++, 22, "CV", "Cabo Verde", 37, metadata: "{\"codigoMH\": \"CV\"}"));
items.Add(Item(id++, 22, "KY", "Caimán, Islas", 38, metadata: "{\"codigoMH\": \"KY\"}"));
items.Add(Item(id++, 22, "KH", "Camboya", 39, metadata: "{\"codigoMH\": \"KH\"}"));
items.Add(Item(id++, 22, "CM", "Camerún", 40, metadata: "{\"codigoMH\": \"CM\"}"));
items.Add(Item(id++, 22, "CA", "Canadá", 41, metadata: "{\"codigoMH\": \"CA\"}"));
items.Add(Item(id++, 22, "CF", "Centroafricana, República", 42, metadata: "{\"codigoMH\": \"CF\"}"));
items.Add(Item(id++, 22, "TD", "Chad", 43, metadata: "{\"codigoMH\": \"TD\"}"));
items.Add(Item(id++, 22, "CL", "Chile", 44, metadata: "{\"codigoMH\": \"CL\"}"));
items.Add(Item(id++, 22, "CN", "China", 45, metadata: "{\"codigoMH\": \"CN\"}"));
items.Add(Item(id++, 22, "CY", "Chipre", 46, metadata: "{\"codigoMH\": \"CY\"}"));
items.Add(Item(id++, 22, "VA", "Ciudad del Vaticano", 47, metadata: "{\"codigoMH\": \"VA\"}"));
items.Add(Item(id++, 22, "CO", "Colombia", 48, metadata: "{\"codigoMH\": \"CO\"}"));
items.Add(Item(id++, 22, "KM", "Comoras", 49, metadata: "{\"codigoMH\": \"KM\"}"));
items.Add(Item(id++, 22, "CG", "Congo", 50, metadata: "{\"codigoMH\": \"CG\"}"));
items.Add(Item(id++, 22, "CI", "Costa de Marfil", 51, metadata: "{\"codigoMH\": \"CI\"}"));
items.Add(Item(id++, 22, "CR", "Costa Rica", 52, metadata: "{\"codigoMH\": \"CR\"}"));
items.Add(Item(id++, 22, "HR", "Croacia", 53, metadata: "{\"codigoMH\": \"HR\"}"));
items.Add(Item(id++, 22, "CU", "Cuba", 54, metadata: "{\"codigoMH\": \"CU\"}"));
items.Add(Item(id++, 22, "CW", "Curazao", 55, metadata: "{\"codigoMH\": \"CW\"}"));
items.Add(Item(id++, 22, "DK", "Dinamarca", 56, metadata: "{\"codigoMH\": \"DK\"}"));
items.Add(Item(id++, 22, "DM", "Dominica", 57, metadata: "{\"codigoMH\": \"DM\"}"));
items.Add(Item(id++, 22, "DJ", "Djiboutí", 58, metadata: "{\"codigoMH\": \"DJ\"}"));
items.Add(Item(id++, 22, "EC", "Ecuador", 59, metadata: "{\"codigoMH\": \"EC\"}"));
items.Add(Item(id++, 22, "EG", "Egipto", 60, metadata: "{\"codigoMH\": \"EG\"}"));
items.Add(Item(id++, 22, "SV", "El Salvador", 61, metadata: "{\"codigoMH\": \"SV\"}"));
items.Add(Item(id++, 22, "AE", "Emiratos Árabes Unidos", 62, metadata: "{\"codigoMH\": \"AE\"}"));
items.Add(Item(id++, 22, "ER", "Eritrea", 63, metadata: "{\"codigoMH\": \"ER\"}"));
items.Add(Item(id++, 22, "SK", "Eslovaquia", 64, metadata: "{\"codigoMH\": \"SK\"}"));
items.Add(Item(id++, 22, "SI", "Eslovenia", 65, metadata: "{\"codigoMH\": \"SI\"}"));
items.Add(Item(id++, 22, "ES", "España", 66, metadata: "{\"codigoMH\": \"ES\"}"));
items.Add(Item(id++, 22, "US", "Estados Unidos", 67, metadata: "{\"codigoMH\": \"US\"}"));
items.Add(Item(id++, 22, "EE", "Estonia", 68, metadata: "{\"codigoMH\": \"EE\"}"));
items.Add(Item(id++, 22, "ET", "Etiopía", 69, metadata: "{\"codigoMH\": \"ET\"}"));
items.Add(Item(id++, 22, "FJ", "Fiji", 70, metadata: "{\"codigoMH\": \"FJ\"}"));
items.Add(Item(id++, 22, "PH", "Filipinas", 71, metadata: "{\"codigoMH\": \"PH\"}"));
items.Add(Item(id++, 22, "FI", "Finlandia", 72, metadata: "{\"codigoMH\": \"FI\"}"));
items.Add(Item(id++, 22, "FR", "Francia", 73, metadata: "{\"codigoMH\": \"FR\"}"));
items.Add(Item(id++, 22, "GA", "Gabón", 74, metadata: "{\"codigoMH\": \"GA\"}"));
items.Add(Item(id++, 22, "GM", "Gambia", 75, metadata: "{\"codigoMH\": \"GM\"}"));
items.Add(Item(id++, 22, "GE", "Georgia", 76, metadata: "{\"codigoMH\": \"GE\"}"));
items.Add(Item(id++, 22, "GH", "Ghana", 77, metadata: "{\"codigoMH\": \"GH\"}"));
items.Add(Item(id++, 22, "GI", "Gibraltar", 78, metadata: "{\"codigoMH\": \"GI\"}"));
items.Add(Item(id++, 22, "GD", "Granada", 79, metadata: "{\"codigoMH\": \"GD\"}"));
items.Add(Item(id++, 22, "GR", "Grecia", 80, metadata: "{\"codigoMH\": \"GR\"}"));
items.Add(Item(id++, 22, "GL", "Groenlandia", 81, metadata: "{\"codigoMH\": \"GL\"}"));
items.Add(Item(id++, 22, "GP", "Guadalupe", 82, metadata: "{\"codigoMH\": \"GP\"}"));
items.Add(Item(id++, 22, "GU", "Guam", 83, metadata: "{\"codigoMH\": \"GU\"}"));
items.Add(Item(id++, 22, "GT", "Guatemala", 84, metadata: "{\"codigoMH\": \"GT\"}"));
items.Add(Item(id++, 22, "GF", "Guayana Francesa", 85, metadata: "{\"codigoMH\": \"GF\"}"));
items.Add(Item(id++, 22, "GG", "Guernsey", 86, metadata: "{\"codigoMH\": \"GG\"}"));
items.Add(Item(id++, 22, "GN", "Guinea", 87, metadata: "{\"codigoMH\": \"GN\"}"));
items.Add(Item(id++, 22, "GQ", "Guinea Ecuatorial", 88, metadata: "{\"codigoMH\": \"GQ\"}"));
items.Add(Item(id++, 22, "GW", "Guinea-Bissau", 89, metadata: "{\"codigoMH\": \"GW\"}"));
items.Add(Item(id++, 22, "GY", "Guyana", 90, metadata: "{\"codigoMH\": \"GY\"}"));
items.Add(Item(id++, 22, "HT", "Haití", 91, metadata: "{\"codigoMH\": \"HT\"}"));
items.Add(Item(id++, 22, "HN", "Honduras", 92, metadata: "{\"codigoMH\": \"HN\"}"));
items.Add(Item(id++, 22, "HK", "Hong Kong", 93, metadata: "{\"codigoMH\": \"HK\"}"));
items.Add(Item(id++, 22, "HU", "Hungría", 94, metadata: "{\"codigoMH\": \"HU\"}"));
items.Add(Item(id++, 22, "IN", "India", 95, metadata: "{\"codigoMH\": \"IN\"}"));
items.Add(Item(id++, 22, "ID", "Indonesia", 96, metadata: "{\"codigoMH\": \"ID\"}"));
items.Add(Item(id++, 22, "IQ", "Irak", 97, metadata: "{\"codigoMH\": \"IQ\"}"));
items.Add(Item(id++, 22, "IE", "Irlanda", 98, metadata: "{\"codigoMH\": \"IE\"}"));
items.Add(Item(id++, 22, "BV", "Isla Bouvet", 99, metadata: "{\"codigoMH\": \"BV\"}"));
items.Add(Item(id++, 22, "IM", "Isla de Man", 100, metadata: "{\"codigoMH\": \"IM\"}"));
items.Add(Item(id++, 22, "NF", "Isla Norfolk", 101, metadata: "{\"codigoMH\": \"NF\"}"));
items.Add(Item(id++, 22, "IS", "Islandia", 102, metadata: "{\"codigoMH\": \"IS\"}"));
items.Add(Item(id++, 22, "CX", "Islas Navidad", 103, metadata: "{\"codigoMH\": \"CX\"}"));
items.Add(Item(id++, 22, "CC", "Islas Cocos", 104, metadata: "{\"codigoMH\": \"CC\"}"));
items.Add(Item(id++, 22, "CK", "Islas Cook", 105, metadata: "{\"codigoMH\": \"CK\"}"));
items.Add(Item(id++, 22, "FO", "Islas Faroe", 106, metadata: "{\"codigoMH\": \"FO\"}"));
items.Add(Item(id++, 22, "GS", "Islas Georgias d. S.-Sandwich d. S.", 107, metadata: "{\"codigoMH\": \"GS\"}"));
items.Add(Item(id++, 22, "HM", "Islas Heard y McDonald", 108, metadata: "{\"codigoMH\": \"HM\"}"));
items.Add(Item(id++, 22, "FK", "Islas Malvinas (Falkland)", 109, metadata: "{\"codigoMH\": \"FK\"}"));
items.Add(Item(id++, 22, "MP", "Islas Marianas del Norte", 110, metadata: "{\"codigoMH\": \"MP\"}"));
items.Add(Item(id++, 22, "MH", "Islas Marshall", 111, metadata: "{\"codigoMH\": \"MH\"}"));
items.Add(Item(id++, 22, "PN", "Islas Pitcairn", 112, metadata: "{\"codigoMH\": \"PN\"}"));
items.Add(Item(id++, 22, "TC", "Islas Turcas y Caicos", 113, metadata: "{\"codigoMH\": \"TC\"}"));
items.Add(Item(id++, 22, "UM", "Islas Ultramarinas de E.E.U.U", 114, metadata: "{\"codigoMH\": \"UM\"}"));
items.Add(Item(id++, 22, "VI", "Islas Vírgenes", 115, metadata: "{\"codigoMH\": \"VI\"}"));
items.Add(Item(id++, 22, "IL", "Israel", 116, metadata: "{\"codigoMH\": \"IL\"}"));
items.Add(Item(id++, 22, "IT", "Italia", 117, metadata: "{\"codigoMH\": \"IT\"}"));
items.Add(Item(id++, 22, "JM", "Jamaica", 118, metadata: "{\"codigoMH\": \"JM\"}"));
items.Add(Item(id++, 22, "JP", "Japón", 119, metadata: "{\"codigoMH\": \"JP\"}"));
items.Add(Item(id++, 22, "JE", "Jersey", 120, metadata: "{\"codigoMH\": \"JE\"}"));
items.Add(Item(id++, 22, "JO", "Jordania", 121, metadata: "{\"codigoMH\": \"JO\"}"));
items.Add(Item(id++, 22, "KZ", "Kazajistán", 122, metadata: "{\"codigoMH\": \"KZ\"}"));
items.Add(Item(id++, 22, "KE", "Kenia", 123, metadata: "{\"codigoMH\": \"KE\"}"));
items.Add(Item(id++, 22, "KG", "Kirguistán", 124, metadata: "{\"codigoMH\": \"KG\"}"));
items.Add(Item(id++, 22, "KI", "Kiribati", 125, metadata: "{\"codigoMH\": \"KI\"}"));
items.Add(Item(id++, 22, "KW", "Kuwait", 126, metadata: "{\"codigoMH\": \"KW\"}"));
items.Add(Item(id++, 22, "LA", "Laos, República Democrática", 127, metadata: "{\"codigoMH\": \"LA\"}"));
items.Add(Item(id++, 22, "LS", "Lesotho", 128, metadata: "{\"codigoMH\": \"LS\"}"));
items.Add(Item(id++, 22, "LV", "Letonia", 129, metadata: "{\"codigoMH\": \"LV\"}"));
items.Add(Item(id++, 22, "LB", "Líbano", 130, metadata: "{\"codigoMH\": \"LB\"}"));
items.Add(Item(id++, 22, "LR", "Liberia", 131, metadata: "{\"codigoMH\": \"LR\"}"));
items.Add(Item(id++, 22, "LY", "Libia", 132, metadata: "{\"codigoMH\": \"LY\"}"));
items.Add(Item(id++, 22, "LI", "Liechtenstein", 133, metadata: "{\"codigoMH\": \"LI\"}"));
items.Add(Item(id++, 22, "LT", "Lituania", 134, metadata: "{\"codigoMH\": \"LT\"}"));
items.Add(Item(id++, 22, "LU", "Luxemburgo", 135, metadata: "{\"codigoMH\": \"LU\"}"));
items.Add(Item(id++, 22, "MO", "Macao", 136, metadata: "{\"codigoMH\": \"MO\"}"));
items.Add(Item(id++, 22, "MK", "Macedonia", 137, metadata: "{\"codigoMH\": \"MK\"}"));
items.Add(Item(id++, 22, "MG", "Madagascar", 138, metadata: "{\"codigoMH\": \"MG\"}"));
items.Add(Item(id++, 22, "MY", "Malasia", 139, metadata: "{\"codigoMH\": \"MY\"}"));
items.Add(Item(id++, 22, "MW", "Malawi", 140, metadata: "{\"codigoMH\": \"MW\"}"));
items.Add(Item(id++, 22, "MV", "Maldivas", 141, metadata: "{\"codigoMH\": \"MV\"}"));
items.Add(Item(id++, 22, "ML", "Malí", 142, metadata: "{\"codigoMH\": \"ML\"}"));
items.Add(Item(id++, 22, "MT", "Malta", 143, metadata: "{\"codigoMH\": \"MT\"}"));
items.Add(Item(id++, 22, "MA", "Marruecos", 144, metadata: "{\"codigoMH\": \"MA\"}"));
items.Add(Item(id++, 22, "MQ", "Martinica e.a.", 145, metadata: "{\"codigoMH\": \"MQ\"}"));
items.Add(Item(id++, 22, "MU", "Mauricio", 146, metadata: "{\"codigoMH\": \"MU\"}"));
items.Add(Item(id++, 22, "MR", "Mauritania", 147, metadata: "{\"codigoMH\": \"MR\"}"));
items.Add(Item(id++, 22, "YT", "Mayotte", 148, metadata: "{\"codigoMH\": \"YT\"}"));
items.Add(Item(id++, 22, "MX", "México", 149, metadata: "{\"codigoMH\": \"MX\"}"));
items.Add(Item(id++, 22, "FM", "Micronesia", 150, metadata: "{\"codigoMH\": \"FM\"}"));
items.Add(Item(id++, 22, "MD", "Moldavia, República de", 151, metadata: "{\"codigoMH\": \"MD\"}"));
items.Add(Item(id++, 22, "MC", "Mónaco", 152, metadata: "{\"codigoMH\": \"MC\"}"));
items.Add(Item(id++, 22, "MN", "Mongolia", 153, metadata: "{\"codigoMH\": \"MN\"}"));
items.Add(Item(id++, 22, "ME", "Montenegro", 154, metadata: "{\"codigoMH\": \"ME\"}"));
items.Add(Item(id++, 22, "MS", "Montserrat", 155, metadata: "{\"codigoMH\": \"MS\"}"));
items.Add(Item(id++, 22, "MZ", "Mozambique", 156, metadata: "{\"codigoMH\": \"MZ\"}"));
items.Add(Item(id++, 22, "MM", "Myanmar", 157, metadata: "{\"codigoMH\": \"MM\"}"));
items.Add(Item(id++, 22, "NA", "Namibia", 158, metadata: "{\"codigoMH\": \"NA\"}"));
items.Add(Item(id++, 22, "NR", "Nauru", 159, metadata: "{\"codigoMH\": \"NR\"}"));
items.Add(Item(id++, 22, "NP", "Nepal", 160, metadata: "{\"codigoMH\": \"NP\"}"));
items.Add(Item(id++, 22, "NI", "Nicaragua", 161, metadata: "{\"codigoMH\": \"NI\"}"));
items.Add(Item(id++, 22, "NE", "Níger", 162, metadata: "{\"codigoMH\": \"NE\"}"));
items.Add(Item(id++, 22, "NG", "Nigeria", 163, metadata: "{\"codigoMH\": \"NG\"}"));
items.Add(Item(id++, 22, "NU", "Niue", 164, metadata: "{\"codigoMH\": \"NU\"}"));
items.Add(Item(id++, 22, "NO", "Noruega", 165, metadata: "{\"codigoMH\": \"NO\"}"));
items.Add(Item(id++, 22, "NC", "Nueva Caledonia", 166, metadata: "{\"codigoMH\": \"NC\"}"));
items.Add(Item(id++, 22, "NZ", "Nueva Zelanda", 167, metadata: "{\"codigoMH\": \"NZ\"}"));
items.Add(Item(id++, 22, "OM", "Omán", 168, metadata: "{\"codigoMH\": \"OM\"}"));
items.Add(Item(id++, 22, "NL", "Países Bajos", 169, metadata: "{\"codigoMH\": \"NL\"}"));
items.Add(Item(id++, 22, "PK", "Pakistán", 170, metadata: "{\"codigoMH\": \"PK\"}"));
items.Add(Item(id++, 22, "PW", "Palaos", 171, metadata: "{\"codigoMH\": \"PW\"}"));
items.Add(Item(id++, 22, "PS", "Palestina", 172, metadata: "{\"codigoMH\": \"PS\"}"));
items.Add(Item(id++, 22, "PA", "Panamá", 173, metadata: "{\"codigoMH\": \"PA\"}"));
items.Add(Item(id++, 22, "PG", "Papúa, Nueva Guinea", 174, metadata: "{\"codigoMH\": \"PG\"}"));
items.Add(Item(id++, 22, "PY", "Paraguay", 175, metadata: "{\"codigoMH\": \"PY\"}"));
items.Add(Item(id++, 22, "PE", "Perú", 176, metadata: "{\"codigoMH\": \"PE\"}"));
items.Add(Item(id++, 22, "PF", "Polinesia Francesa", 177, metadata: "{\"codigoMH\": \"PF\"}"));
items.Add(Item(id++, 22, "PL", "Polonia", 178, metadata: "{\"codigoMH\": \"PL\"}"));
items.Add(Item(id++, 22, "PT", "Portugal", 179, metadata: "{\"codigoMH\": \"PT\"}"));
items.Add(Item(id++, 22, "PR", "Puerto Rico", 180, metadata: "{\"codigoMH\": \"PR\"}"));
items.Add(Item(id++, 22, "QA", "Qatar", 181, metadata: "{\"codigoMH\": \"QA\"}"));
items.Add(Item(id++, 22, "GB", "Reino Unido", 182, metadata: "{\"codigoMH\": \"GB\"}"));
items.Add(Item(id++, 22, "KP", "Rep. Democrática popular de Corea", 183, metadata: "{\"codigoMH\": \"KP\"}"));
items.Add(Item(id++, 22, "CZ", "República Checa", 184, metadata: "{\"codigoMH\": \"CZ\"}"));
items.Add(Item(id++, 22, "KR", "República de Corea", 185, metadata: "{\"codigoMH\": \"KR\"}"));
items.Add(Item(id++, 22, "CD", "República Democrática del Congo", 186, metadata: "{\"codigoMH\": \"CD\"}"));
items.Add(Item(id++, 22, "DO", "República Dominicana", 187, metadata: "{\"codigoMH\": \"DO\"}"));
items.Add(Item(id++, 22, "IR", "República Islámica de Irán", 188, metadata: "{\"codigoMH\": \"IR\"}"));
items.Add(Item(id++, 22, "RE", "Reunión", 189, metadata: "{\"codigoMH\": \"RE\"}"));
items.Add(Item(id++, 22, "RW", "Ruanda", 190, metadata: "{\"codigoMH\": \"RW\"}"));
items.Add(Item(id++, 22, "RO", "Rumania", 191, metadata: "{\"codigoMH\": \"RO\"}"));
items.Add(Item(id++, 22, "RU", "Rusia", 192, metadata: "{\"codigoMH\": \"RU\"}"));
items.Add(Item(id++, 22, "EH", "Sahara Occidental", 193, metadata: "{\"codigoMH\": \"EH\"}"));
items.Add(Item(id++, 22, "BL", "Saint Barthélemy", 194, metadata: "{\"codigoMH\": \"BL\"}"));
items.Add(Item(id++, 22, "MF", "Saint Martin (French part)", 195, metadata: "{\"codigoMH\": \"MF\"}"));
items.Add(Item(id++, 22, "SB", "Salomón, Islas", 196, metadata: "{\"codigoMH\": \"SB\"}"));
items.Add(Item(id++, 22, "WS", "Samoa", 197, metadata: "{\"codigoMH\": \"WS\"}"));
items.Add(Item(id++, 22, "AS", "Samoa Americana", 198, metadata: "{\"codigoMH\": \"AS\"}"));
items.Add(Item(id++, 22, "KN", "San Cristóbal y Nieves", 199, metadata: "{\"codigoMH\": \"KN\"}"));
items.Add(Item(id++, 22, "SM", "San Marino", 200, metadata: "{\"codigoMH\": \"SM\"}"));
items.Add(Item(id++, 22, "PM", "San Pedro y Miquelón", 201, metadata: "{\"codigoMH\": \"PM\"}"));
items.Add(Item(id++, 22, "VC", "San Vicente y las Granadinas", 202, metadata: "{\"codigoMH\": \"VC\"}"));
items.Add(Item(id++, 22, "SH", "Santa Elena", 203, metadata: "{\"codigoMH\": \"SH\"}"));
items.Add(Item(id++, 22, "LC", "Santa Lucía", 204, metadata: "{\"codigoMH\": \"LC\"}"));
items.Add(Item(id++, 22, "ST", "Santo Tomé y Príncipe", 205, metadata: "{\"codigoMH\": \"ST\"}"));
items.Add(Item(id++, 22, "SN", "Senegal", 206, metadata: "{\"codigoMH\": \"SN\"}"));
items.Add(Item(id++, 22, "RS", "Serbia", 207, metadata: "{\"codigoMH\": \"RS\"}"));
items.Add(Item(id++, 22, "SC", "Seychelles", 208, metadata: "{\"codigoMH\": \"SC\"}"));
items.Add(Item(id++, 22, "SL", "Sierra Leona", 209, metadata: "{\"codigoMH\": \"SL\"}"));
items.Add(Item(id++, 22, "SG", "Singapur", 210, metadata: "{\"codigoMH\": \"SG\"}"));
items.Add(Item(id++, 22, "SX", "Sint Maarten (Dutch part)", 211, metadata: "{\"codigoMH\": \"SX\"}"));
items.Add(Item(id++, 22, "SY", "Siria", 212, metadata: "{\"codigoMH\": \"SY\"}"));
items.Add(Item(id++, 22, "SO", "Somalia", 213, metadata: "{\"codigoMH\": \"SO\"}"));
items.Add(Item(id++, 22, "SS", "South Sudan", 214, metadata: "{\"codigoMH\": \"SS\"}"));
items.Add(Item(id++, 22, "LK", "Sri Lanka", 215, metadata: "{\"codigoMH\": \"LK\"}"));
items.Add(Item(id++, 22, "ZA", "Sudáfrica", 216, metadata: "{\"codigoMH\": \"ZA\"}"));
items.Add(Item(id++, 22, "SD", "Sudán", 217, metadata: "{\"codigoMH\": \"SD\"}"));
items.Add(Item(id++, 22, "SE", "Suecia", 218, metadata: "{\"codigoMH\": \"SE\"}"));
items.Add(Item(id++, 22, "CH", "Suiza", 219, metadata: "{\"codigoMH\": \"CH\"}"));
items.Add(Item(id++, 22, "SR", "Surinám", 220, metadata: "{\"codigoMH\": \"SR\"}"));
items.Add(Item(id++, 22, "SJ", "Svalbard y Jan Mayen", 221, metadata: "{\"codigoMH\": \"SJ\"}"));
items.Add(Item(id++, 22, "SZ", "Swazilandia", 222, metadata: "{\"codigoMH\": \"SZ\"}"));
items.Add(Item(id++, 22, "TH", "Tailandia", 223, metadata: "{\"codigoMH\": \"TH\"}"));
items.Add(Item(id++, 22, "TW", "Taiwan, Provincia de China", 224, metadata: "{\"codigoMH\": \"TW\"}"));
items.Add(Item(id++, 22, "TZ", "Tanzania, República Unida de", 225, metadata: "{\"codigoMH\": \"TZ\"}"));
items.Add(Item(id++, 22, "TJ", "Tayikistán", 226, metadata: "{\"codigoMH\": \"TJ\"}"));
items.Add(Item(id++, 22, "IO", "Territorio Británico Océano Indico", 227, metadata: "{\"codigoMH\": \"IO\"}"));
items.Add(Item(id++, 22, "TF", "Territorios Australes Franceses", 228, metadata: "{\"codigoMH\": \"TF\"}"));
items.Add(Item(id++, 22, "TL", "Timor Oriental", 229, metadata: "{\"codigoMH\": \"TL\"}"));
items.Add(Item(id++, 22, "TG", "Togo", 230, metadata: "{\"codigoMH\": \"TG\"}"));
items.Add(Item(id++, 22, "TK", "Tokelau", 231, metadata: "{\"codigoMH\": \"TK\"}"));
items.Add(Item(id++, 22, "TO", "Tonga", 232, metadata: "{\"codigoMH\": \"TO\"}"));
items.Add(Item(id++, 22, "TT", "Trinidad y Tobago", 233, metadata: "{\"codigoMH\": \"TT\"}"));
items.Add(Item(id++, 22, "TN", "Túnez", 234, metadata: "{\"codigoMH\": \"TN\"}"));
items.Add(Item(id++, 22, "TM", "Turkmenistán", 235, metadata: "{\"codigoMH\": \"TM\"}"));
items.Add(Item(id++, 22, "TR", "Turquía", 236, metadata: "{\"codigoMH\": \"TR\"}"));
items.Add(Item(id++, 22, "TV", "Tuvalu", 237, metadata: "{\"codigoMH\": \"TV\"}"));
items.Add(Item(id++, 22, "UA", "Ucrania", 238, metadata: "{\"codigoMH\": \"UA\"}"));
items.Add(Item(id++, 22, "UG", "Uganda", 239, metadata: "{\"codigoMH\": \"UG\"}"));
items.Add(Item(id++, 22, "UY", "Uruguay", 240, metadata: "{\"codigoMH\": \"UY\"}"));
items.Add(Item(id++, 22, "UZ", "Uzbekistán", 241, metadata: "{\"codigoMH\": \"UZ\"}"));
items.Add(Item(id++, 22, "VU", "Vanuatu", 242, metadata: "{\"codigoMH\": \"VU\"}"));
items.Add(Item(id++, 22, "VE", "Venezuela", 243, metadata: "{\"codigoMH\": \"VE\"}"));
items.Add(Item(id++, 22, "VN", "Vietnam", 244, metadata: "{\"codigoMH\": \"VN\"}"));
items.Add(Item(id++, 22, "VG", "Islas Vírgenes Británicas", 245, metadata: "{\"codigoMH\": \"VG\"}"));
items.Add(Item(id++, 22, "WF", "Wallis y Fortuna, Islas", 246, metadata: "{\"codigoMH\": \"WF\"}"));
items.Add(Item(id++, 22, "YE", "Yemen", 247, metadata: "{\"codigoMH\": \"YE\"}"));
items.Add(Item(id++, 22, "ZM", "Zambia", 248, metadata: "{\"codigoMH\": \"ZM\"}"));
items.Add(Item(id++, 22, "ZW", "Zimbabue", 249, metadata: "{\"codigoMH\": \"ZW\"}"));

// Padding para preservar la secuencia de IDs de catálogos posteriores tras pasar de
// 275 (CAT-020 legacy 4 dígitos) a 249 países (CAT-020 v1.1 ISO 3166-1 alfa-2).
id += 26;

// ----- CAT-027 RECINTO_FISCAL (catId=32) -----
items.Add(Item(id++, 32, "01", "Terrestre San Bartolo", 1, metadata: "{\"codigoMH\": \"01\"}"));
items.Add(Item(id++, 32, "02", "Marítima de Acajutla", 2, metadata: "{\"codigoMH\": \"02\"}"));
items.Add(Item(id++, 32, "03", "Aérea Monseñor Óscar Arnulfo Romero", 3, metadata: "{\"codigoMH\": \"03\"}"));
items.Add(Item(id++, 32, "04", "Terrestre Las Chinamas", 4, metadata: "{\"codigoMH\": \"04\"}"));
items.Add(Item(id++, 32, "05", "Terrestre La Hachadura", 5, metadata: "{\"codigoMH\": \"05\"}"));
items.Add(Item(id++, 32, "06", "Terrestre Santa Ana", 6, metadata: "{\"codigoMH\": \"06\"}"));
items.Add(Item(id++, 32, "07", "Terrestre San Cristóbal", 7, metadata: "{\"codigoMH\": \"07\"}"));
items.Add(Item(id++, 32, "08", "Terrestre Anguiatú", 8, metadata: "{\"codigoMH\": \"08\"}"));
items.Add(Item(id++, 32, "09", "Terrestre El Amatillo", 9, metadata: "{\"codigoMH\": \"09\"}"));
items.Add(Item(id++, 32, "10", "Marítima La Unión (Puerto Cutuco)", 10, metadata: "{\"codigoMH\": \"10\"}"));
items.Add(Item(id++, 32, "11", "Terrestre El Poy", 11, metadata: "{\"codigoMH\": \"11\"}"));
items.Add(Item(id++, 32, "12", "Aduana Terrestre Metalío", 12, metadata: "{\"codigoMH\": \"12\"}"));
items.Add(Item(id++, 32, "15", "Fardos Postales", 13, metadata: "{\"codigoMH\": \"15\"}"));
items.Add(Item(id++, 32, "16", "Z.F. San Marcos", 14, metadata: "{\"codigoMH\": \"16\"}"));
items.Add(Item(id++, 32, "17", "Z.F. El Pedregal", 15, metadata: "{\"codigoMH\": \"17\"}"));
items.Add(Item(id++, 32, "18", "Z.F. San Bartolo", 16, metadata: "{\"codigoMH\": \"18\"}"));
items.Add(Item(id++, 32, "20", "Z.F. Exportsalva", 17, metadata: "{\"codigoMH\": \"20\"}"));
items.Add(Item(id++, 32, "21", "Z.F. American Park", 18, metadata: "{\"codigoMH\": \"21\"}"));
items.Add(Item(id++, 32, "23", "Z.F. Internacional", 19, metadata: "{\"codigoMH\": \"23\"}"));
items.Add(Item(id++, 32, "24", "Z.F. Diez", 20, metadata: "{\"codigoMH\": \"24\"}"));
items.Add(Item(id++, 32, "26", "Z.F. Miramar", 21, metadata: "{\"codigoMH\": \"26\"}"));
items.Add(Item(id++, 32, "27", "Z.F. Santo Tomas", 22, metadata: "{\"codigoMH\": \"27\"}"));
items.Add(Item(id++, 32, "28", "Z.F. Santa Tecla", 23, metadata: "{\"codigoMH\": \"28\"}"));
items.Add(Item(id++, 32, "29", "Z.F. Santa Ana", 24, metadata: "{\"codigoMH\": \"29\"}"));
items.Add(Item(id++, 32, "30", "Z.F. La Concordia", 25, metadata: "{\"codigoMH\": \"30\"}"));
items.Add(Item(id++, 32, "31", "Aérea Ilopango", 26, metadata: "{\"codigoMH\": \"31\"}"));
items.Add(Item(id++, 32, "32", "Z.F. Pipil", 27, metadata: "{\"codigoMH\": \"32\"}"));
items.Add(Item(id++, 32, "33", "Puerto Barillas", 28, metadata: "{\"codigoMH\": \"33\"}"));
items.Add(Item(id++, 32, "34", "Z.F. Calvo Conservas", 29, metadata: "{\"codigoMH\": \"34\"}"));
items.Add(Item(id++, 32, "35", "Feria Internacional", 30, metadata: "{\"codigoMH\": \"35\"}"));
items.Add(Item(id++, 32, "36", "Delg. Aduana El Papalón", 31, metadata: "{\"codigoMH\": \"36\"}"));
items.Add(Item(id++, 32, "37", "Z.F. Parque Industrial Sam-Li", 32, metadata: "{\"codigoMH\": \"37\"}"));
items.Add(Item(id++, 32, "38", "Z.F. San José", 33, metadata: "{\"codigoMH\": \"38\"}"));
items.Add(Item(id++, 32, "39", "Z.F. Las Mercedes", 34, metadata: "{\"codigoMH\": \"39\"}"));
items.Add(Item(id++, 32, "40", "Z.F. EMCO", 35, metadata: "{\"codigoMH\": \"40\"}"));
items.Add(Item(id++, 32, "41", "Z.F. Gigante", 36, metadata: "{\"codigoMH\": \"41\"}"));
items.Add(Item(id++, 32, "71", "Almacenes De Desarrollo (Aldesa)", 37, metadata: "{\"codigoMH\": \"71\"}"));
items.Add(Item(id++, 32, "72", "Almac. Gral. Dep. Occidente (Agdosa)", 38, metadata: "{\"codigoMH\": \"72\"}"));
items.Add(Item(id++, 32, "73", "Bodega General De Depósito (Bodesa)", 39, metadata: "{\"codigoMH\": \"73\"}"));
items.Add(Item(id++, 32, "76", "DHL", 40, metadata: "{\"codigoMH\": \"76\"}"));
items.Add(Item(id++, 32, "77", "Transauto (Santa Elena)", 41, metadata: "{\"codigoMH\": \"77\"}"));
items.Add(Item(id++, 32, "80", "Almacenadora Nejapa, S.A. de C.V.", 42, metadata: "{\"codigoMH\": \"80\"}"));
items.Add(Item(id++, 32, "81", "Almacenadora Almaconsa S.A. de C.V.", 43, metadata: "{\"codigoMH\": \"81\"}"));
items.Add(Item(id++, 32, "83", "Alm.Gral. Depósito Occidente (Apopa)", 44, metadata: "{\"codigoMH\": \"83\"}"));
items.Add(Item(id++, 32, "99", "San Bartolo Envío Hn/Gt", 45, metadata: "{\"codigoMH\": \"99\"}"));

// ----- CAT-021 OTRO_DOC_ASOCIADO (nuevo) (catId=28) -----
items.Add(Item(id++, 28, "1", "Emisor", 1, metadata: "{\"codigoMH\": \"1\"}"));
items.Add(Item(id++, 28, "2", "Receptor", 2, metadata: "{\"codigoMH\": \"2\"}"));
items.Add(Item(id++, 28, "3", "Médico (solo aplica para contribuyentes obligados a la presentación de F-958)", 3, metadata: "{\"codigoMH\": \"3\"}"));
items.Add(Item(id++, 28, "4", "Transporte (solo aplica para Factura de Exportación)", 4, metadata: "{\"codigoMH\": \"4\"}"));

// ----- CAT-022 TIPO_DOC_IDENTIDAD (reemplaza) (catId=7) -----
items.Add(Item(id++, 7, "36", "NIT", 1, metadata: "{\"codigoMH\": \"36\"}"));
items.Add(Item(id++, 7, "13", "DUI", 2, metadata: "{\"codigoMH\": \"13\"}"));
items.Add(Item(id++, 7, "37", "Otro", 3, metadata: "{\"codigoMH\": \"37\"}"));
items.Add(Item(id++, 7, "03", "Pasaporte", 4, metadata: "{\"codigoMH\": \"03\"}"));
items.Add(Item(id++, 7, "02", "Carnet de Residente", 5, metadata: "{\"codigoMH\": \"02\"}"));

// ----- CAT-023 TIPO_DOC_CONTINGENCIA (nuevo) (catId=29) -----
items.Add(Item(id++, 29, "01", "Factura Electrónica", 1, metadata: "{\"codigoMH\": \"01\"}"));
items.Add(Item(id++, 29, "03", "Comprobante de Crédito Fiscal Electrónico", 2, metadata: "{\"codigoMH\": \"03\"}"));
items.Add(Item(id++, 29, "04", "Nota de Remisión Electrónica", 3, metadata: "{\"codigoMH\": \"04\"}"));
items.Add(Item(id++, 29, "05", "Nota de Crédito Electrónica", 4, metadata: "{\"codigoMH\": \"05\"}"));
items.Add(Item(id++, 29, "06", "Nota de Débito Electrónica", 5, metadata: "{\"codigoMH\": \"06\"}"));
items.Add(Item(id++, 29, "11", "Factura de Exportación Electrónica", 6, metadata: "{\"codigoMH\": \"11\"}"));
items.Add(Item(id++, 29, "14", "Factura de Sujeto Excluido Electrónica", 7, metadata: "{\"codigoMH\": \"14\"}"));

// ----- CAT-024 MOTIVO_INVALIDACION (reemplaza) (catId=23) -----
items.Add(Item(id++, 23, "1", "Error en la información del Documento Tributario Electrónico a invalidar.", 1, metadata: "{\"codigoMH\": \"1\"}"));
items.Add(Item(id++, 23, "2", "Rescindir de la operación realizada.", 2, metadata: "{\"codigoMH\": \"2\"}"));
items.Add(Item(id++, 23, "3", "Otro", 3, metadata: "{\"codigoMH\": \"3\"}"));

// ----- CAT-025 TITULO_REMISION (nuevo) (catId=30) -----
items.Add(Item(id++, 30, "01", "Depósito", 1, metadata: "{\"codigoMH\": \"01\"}"));
items.Add(Item(id++, 30, "02", "Propiedad", 2, metadata: "{\"codigoMH\": \"02\"}"));
items.Add(Item(id++, 30, "03", "Consignación", 3, metadata: "{\"codigoMH\": \"03\"}"));
items.Add(Item(id++, 30, "04", "Traslado", 4, metadata: "{\"codigoMH\": \"04\"}"));
items.Add(Item(id++, 30, "05", "Otros", 5, metadata: "{\"codigoMH\": \"05\"}"));

// ----- CAT-026 TIPO_DONACION (nuevo) (catId=31) -----
items.Add(Item(id++, 31, "1", "Efectivo", 1, metadata: "{\"codigoMH\": \"1\"}"));
items.Add(Item(id++, 31, "2", "Bien", 2, metadata: "{\"codigoMH\": \"2\"}"));
items.Add(Item(id++, 31, "3", "Servicio", 3, metadata: "{\"codigoMH\": \"3\"}"));

// ----- CAT-029 TIPO_PERSONA (catId=33) -----
items.Add(Item(id++, 33, "1", "Persona Natural", 1, metadata: "{\"codigoMH\": \"1\"}"));
items.Add(Item(id++, 33, "2", "Persona Jurídica", 2, metadata: "{\"codigoMH\": \"2\"}"));

// ----- CAT-030 TRANSPORTE (catId=34) -----
items.Add(Item(id++, 34, "1", "Terrestre", 1, metadata: "{\"codigoMH\": \"1\"}"));
items.Add(Item(id++, 34, "2", "Marítimo", 2, metadata: "{\"codigoMH\": \"2\"}"));
items.Add(Item(id++, 34, "3", "Aéreo", 3, metadata: "{\"codigoMH\": \"3\"}"));
items.Add(Item(id++, 34, "4", "Multimodal, Terrestre-marítimo", 4, metadata: "{\"codigoMH\": \"4\"}"));
items.Add(Item(id++, 34, "5", "Multimodal, Terrestre-aéreo", 5, metadata: "{\"codigoMH\": \"5\"}"));
items.Add(Item(id++, 34, "6", "Multimodal, Marítimo-aéreo", 6, metadata: "{\"codigoMH\": \"6\"}"));
items.Add(Item(id++, 34, "7", "Multimodal, Terrestre-Marítimo-aéreo", 7, metadata: "{\"codigoMH\": \"7\"}"));

// ----- CAT-031 INCOTERMS (catId=35) -----
items.Add(Item(id++, 35, "01", "EXW-En fábrica", 1, metadata: "{\"codigoMH\": \"01\"}"));
items.Add(Item(id++, 35, "02", "FCA-Libre transportista", 2, metadata: "{\"codigoMH\": \"02\"}"));
items.Add(Item(id++, 35, "03", "CPT-Transporte pagado hasta", 3, metadata: "{\"codigoMH\": \"03\"}"));
items.Add(Item(id++, 35, "04", "CIP-Transporte y seguro pagado hasta", 4, metadata: "{\"codigoMH\": \"04\"}"));
items.Add(Item(id++, 35, "05", "DAP-Entrega en el lugar", 5, metadata: "{\"codigoMH\": \"05\"}"));
items.Add(Item(id++, 35, "06", "DPU-Entregado en el lugar descargado", 6, metadata: "{\"codigoMH\": \"06\"}"));
items.Add(Item(id++, 35, "07", "DDP-Entrega con impuestos pagados", 7, metadata: "{\"codigoMH\": \"07\"}"));
items.Add(Item(id++, 35, "08", "FAS-Libre al costado del buque", 8, metadata: "{\"codigoMH\": \"08\"}"));
items.Add(Item(id++, 35, "09", "FOB-Libre a bordo", 9, metadata: "{\"codigoMH\": \"09\"}"));
items.Add(Item(id++, 35, "10", "CFR-Costo y flete", 10, metadata: "{\"codigoMH\": \"10\"}"));
items.Add(Item(id++, 35, "11", "CIF-Costo seguro y flete", 11, metadata: "{\"codigoMH\": \"11\"}"));
items.Add(Item(id++, 35, "12", "DAT-Entregado en terminal", 12, metadata: "{\"codigoMH\": \"12\"}"));
items.Add(Item(id++, 35, "13", "DAF-Entregada en frontera", 13, metadata: "{\"codigoMH\": \"13\"}"));
items.Add(Item(id++, 35, "14", "DES-Entregada sobre buque", 14, metadata: "{\"codigoMH\": \"14\"}"));
items.Add(Item(id++, 35, "15", "DEQ-Entregada en muelle", 15, metadata: "{\"codigoMH\": \"15\"}"));
items.Add(Item(id++, 35, "16", "DDU-Entregada derechos no pagados", 16, metadata: "{\"codigoMH\": \"16\"}"));

// ----- CAT-032 DOMICILIO_FISCAL (catId=36) -----
items.Add(Item(id++, 36, "1", "Domiciliado", 1, metadata: "{\"codigoMH\": \"1\"}"));
items.Add(Item(id++, 36, "2", "No Domiciliado", 2, metadata: "{\"codigoMH\": \"2\"}"));

    }
}
