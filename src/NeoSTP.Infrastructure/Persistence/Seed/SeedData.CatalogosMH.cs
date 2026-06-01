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
items.Add(Item(id++, 22, "9300", "El Salvador", 1, metadata: "{\"codigoMH\": \"9300\", \"nombreMH\": \"EL SALVADOR\"}"));
items.Add(Item(id++, 22, "9303", "Afganistán", 2, metadata: "{\"codigoMH\": \"9303\", \"nombreMH\": \"AFGANISTÁN\"}"));
items.Add(Item(id++, 22, "9304", "Aland", 3, metadata: "{\"codigoMH\": \"9304\", \"nombreMH\": \"ALAND\"}"));
items.Add(Item(id++, 22, "9306", "Albania", 4, metadata: "{\"codigoMH\": \"9306\", \"nombreMH\": \"ALBANIA\"}"));
items.Add(Item(id++, 22, "9309", "Alemania Occid", 5, metadata: "{\"codigoMH\": \"9309\", \"nombreMH\": \"ALEMANIA OCCID\"}"));
items.Add(Item(id++, 22, "9310", "Alemania Orient", 6, metadata: "{\"codigoMH\": \"9310\", \"nombreMH\": \"ALEMANIA ORIENT\"}"));
items.Add(Item(id++, 22, "9311", "Alemania", 7, metadata: "{\"codigoMH\": \"9311\", \"nombreMH\": \"ALEMANIA\"}"));
items.Add(Item(id++, 22, "9315", "Alto Volta", 8, metadata: "{\"codigoMH\": \"9315\", \"nombreMH\": \"ALTO VOLTA\"}"));
items.Add(Item(id++, 22, "9317", "Andorra", 9, metadata: "{\"codigoMH\": \"9317\", \"nombreMH\": \"ANDORRA\"}"));
items.Add(Item(id++, 22, "9318", "Angola", 10, metadata: "{\"codigoMH\": \"9318\", \"nombreMH\": \"ANGOLA\"}"));
items.Add(Item(id++, 22, "9319", "Antig Y Barbuda", 11, metadata: "{\"codigoMH\": \"9319\", \"nombreMH\": \"ANTIG Y BARBUDA\"}"));
items.Add(Item(id++, 22, "9320", "Anguila", 12, metadata: "{\"codigoMH\": \"9320\", \"nombreMH\": \"ANGUILA\"}"));
items.Add(Item(id++, 22, "9324", "Arabia Saudita", 13, metadata: "{\"codigoMH\": \"9324\", \"nombreMH\": \"ARABIA SAUDITA\"}"));
items.Add(Item(id++, 22, "9327", "Argelia", 14, metadata: "{\"codigoMH\": \"9327\", \"nombreMH\": \"ARGELIA\"}"));
items.Add(Item(id++, 22, "9330", "Argentina", 15, metadata: "{\"codigoMH\": \"9330\", \"nombreMH\": \"ARGENTINA\"}"));
items.Add(Item(id++, 22, "9332", "Aruba", 16, metadata: "{\"codigoMH\": \"9332\", \"nombreMH\": \"ARUBA\"}"));
items.Add(Item(id++, 22, "9333", "Australia", 17, metadata: "{\"codigoMH\": \"9333\", \"nombreMH\": \"AUSTRALIA\"}"));
items.Add(Item(id++, 22, "9336", "Austria", 18, metadata: "{\"codigoMH\": \"9336\", \"nombreMH\": \"AUSTRIA\"}"));
items.Add(Item(id++, 22, "9338", "Azerbaiyán", 19, metadata: "{\"codigoMH\": \"9338\", \"nombreMH\": \"AZERBAIYÁN\"}"));
items.Add(Item(id++, 22, "9339", "Bangladesh", 20, metadata: "{\"codigoMH\": \"9339\", \"nombreMH\": \"BANGLADESH\"}"));
items.Add(Item(id++, 22, "9342", "Bahréin", 21, metadata: "{\"codigoMH\": \"9342\", \"nombreMH\": \"BAHRÉIN\"}"));
items.Add(Item(id++, 22, "9345", "Barbados", 22, metadata: "{\"codigoMH\": \"9345\", \"nombreMH\": \"BARBADOS\"}"));
items.Add(Item(id++, 22, "9348", "Bélgica", 23, metadata: "{\"codigoMH\": \"9348\", \"nombreMH\": \"BÉLGICA\"}"));
items.Add(Item(id++, 22, "9349", "Belice", 24, metadata: "{\"codigoMH\": \"9349\", \"nombreMH\": \"BELICE\"}"));
items.Add(Item(id++, 22, "9350", "Benín", 25, metadata: "{\"codigoMH\": \"9350\", \"nombreMH\": \"BENÍN\"}"));
items.Add(Item(id++, 22, "9353", "Bielorrusia", 26, metadata: "{\"codigoMH\": \"9353\", \"nombreMH\": \"BIELORRUSIA\"}"));
items.Add(Item(id++, 22, "9354", "Birmania", 27, metadata: "{\"codigoMH\": \"9354\", \"nombreMH\": \"BIRMANIA\"}"));
items.Add(Item(id++, 22, "9357", "Bolivia", 28, metadata: "{\"codigoMH\": \"9357\", \"nombreMH\": \"BOLIVIA\"}"));
items.Add(Item(id++, 22, "9359", "Bosnia Y Herzegovina", 29, metadata: "{\"codigoMH\": \"9359\", \"nombreMH\": \"BOSNIA Y HERZEGOVINA\"}"));
items.Add(Item(id++, 22, "9360", "Botswana", 30, metadata: "{\"codigoMH\": \"9360\", \"nombreMH\": \"BOTSWANA\"}"));
items.Add(Item(id++, 22, "9363", "Brasil", 31, metadata: "{\"codigoMH\": \"9363\", \"nombreMH\": \"BRASIL\"}"));
items.Add(Item(id++, 22, "9366", "Brunéi", 32, metadata: "{\"codigoMH\": \"9366\", \"nombreMH\": \"BRUNÉI\"}"));
items.Add(Item(id++, 22, "9369", "Bulgaria", 33, metadata: "{\"codigoMH\": \"9369\", \"nombreMH\": \"BULGARIA\"}"));
items.Add(Item(id++, 22, "9371", "Burkina Faso", 34, metadata: "{\"codigoMH\": \"9371\", \"nombreMH\": \"BURKINA FASO\"}"));
items.Add(Item(id++, 22, "9372", "Burundi", 35, metadata: "{\"codigoMH\": \"9372\", \"nombreMH\": \"BURUNDI\"}"));
items.Add(Item(id++, 22, "9374", "Bophuthatswana", 36, metadata: "{\"codigoMH\": \"9374\", \"nombreMH\": \"BOPHUTHATSWANA\"}"));
items.Add(Item(id++, 22, "9375", "Bután", 37, metadata: "{\"codigoMH\": \"9375\", \"nombreMH\": \"BUTÁN\"}"));
items.Add(Item(id++, 22, "9376", "Cabinda", 38, metadata: "{\"codigoMH\": \"9376\", \"nombreMH\": \"CABINDA\"}"));
items.Add(Item(id++, 22, "9377", "Cabo Verde", 39, metadata: "{\"codigoMH\": \"9377\", \"nombreMH\": \"CABO VERDE\"}"));
items.Add(Item(id++, 22, "9378", "Camboya", 40, metadata: "{\"codigoMH\": \"9378\", \"nombreMH\": \"CAMBOYA\"}"));
items.Add(Item(id++, 22, "9381", "Camerún", 41, metadata: "{\"codigoMH\": \"9381\", \"nombreMH\": \"CAMERÚN\"}"));
items.Add(Item(id++, 22, "9384", "Canadá", 42, metadata: "{\"codigoMH\": \"9384\", \"nombreMH\": \"CANADÁ\"}"));
items.Add(Item(id++, 22, "9387", "Ceilán", 43, metadata: "{\"codigoMH\": \"9387\", \"nombreMH\": \"CEILÁN\"}"));
items.Add(Item(id++, 22, "9390", "Ctro Afric Rep", 44, metadata: "{\"codigoMH\": \"9390\", \"nombreMH\": \"CTRO AFRIC REP\"}"));
items.Add(Item(id++, 22, "9393", "Colombia", 45, metadata: "{\"codigoMH\": \"9393\", \"nombreMH\": \"COLOMBIA\"}"));
items.Add(Item(id++, 22, "9394", "Comoras-Islas", 46, metadata: "{\"codigoMH\": \"9394\", \"nombreMH\": \"COMORAS-ISLAS\"}"));
items.Add(Item(id++, 22, "9396", "Congo Rep Del", 47, metadata: "{\"codigoMH\": \"9396\", \"nombreMH\": \"CONGO REP DEL\"}"));
items.Add(Item(id++, 22, "9399", "Congo Rep Democ", 48, metadata: "{\"codigoMH\": \"9399\", \"nombreMH\": \"CONGO REP DEMOC\"}"));
items.Add(Item(id++, 22, "9402", "Corea Norte", 49, metadata: "{\"codigoMH\": \"9402\", \"nombreMH\": \"COREA NORTE\"}"));
items.Add(Item(id++, 22, "9405", "Corea Sur", 50, metadata: "{\"codigoMH\": \"9405\", \"nombreMH\": \"COREA SUR\"}"));
items.Add(Item(id++, 22, "9408", "Costa De Marfil", 51, metadata: "{\"codigoMH\": \"9408\", \"nombreMH\": \"COSTA DE MARFIL\"}"));
items.Add(Item(id++, 22, "9411", "Costa Rica", 52, metadata: "{\"codigoMH\": \"9411\", \"nombreMH\": \"COSTA RICA\"}"));
items.Add(Item(id++, 22, "9414", "Cuba", 53, metadata: "{\"codigoMH\": \"9414\", \"nombreMH\": \"CUBA\"}"));
items.Add(Item(id++, 22, "9415", "Curazao", 54, metadata: "{\"codigoMH\": \"9415\", \"nombreMH\": \"CURAZAO\"}"));
items.Add(Item(id++, 22, "9417", "Chad", 55, metadata: "{\"codigoMH\": \"9417\", \"nombreMH\": \"CHAD\"}"));
items.Add(Item(id++, 22, "9420", "Checoslovaquia", 56, metadata: "{\"codigoMH\": \"9420\", \"nombreMH\": \"CHECOSLOVAQUIA\"}"));
items.Add(Item(id++, 22, "9423", "Chile", 57, metadata: "{\"codigoMH\": \"9423\", \"nombreMH\": \"CHILE\"}"));
items.Add(Item(id++, 22, "9426", "China Rep Popul", 58, metadata: "{\"codigoMH\": \"9426\", \"nombreMH\": \"CHINA REP POPUL\"}"));
items.Add(Item(id++, 22, "9432", "Chipre", 59, metadata: "{\"codigoMH\": \"9432\", \"nombreMH\": \"CHIPRE\"}"));
items.Add(Item(id++, 22, "9435", "Dahomey", 60, metadata: "{\"codigoMH\": \"9435\", \"nombreMH\": \"DAHOMEY\"}"));
items.Add(Item(id++, 22, "9438", "Dinamarca", 61, metadata: "{\"codigoMH\": \"9438\", \"nombreMH\": \"DINAMARCA\"}"));
items.Add(Item(id++, 22, "9439", "Djibouti", 62, metadata: "{\"codigoMH\": \"9439\", \"nombreMH\": \"DJIBOUTI\"}"));
items.Add(Item(id++, 22, "9440", "Dominica", 63, metadata: "{\"codigoMH\": \"9440\", \"nombreMH\": \"DOMINICA\"}"));
items.Add(Item(id++, 22, "9441", "Dominicana Rep", 64, metadata: "{\"codigoMH\": \"9441\", \"nombreMH\": \"DOMINICANA REP\"}"));
items.Add(Item(id++, 22, "9444", "Ecuador", 65, metadata: "{\"codigoMH\": \"9444\", \"nombreMH\": \"ECUADOR\"}"));
items.Add(Item(id++, 22, "9446", "Emirat Arab Uni", 66, metadata: "{\"codigoMH\": \"9446\", \"nombreMH\": \"EMIRAT ARAB UNI\"}"));
items.Add(Item(id++, 22, "9447", "España", 67, metadata: "{\"codigoMH\": \"9447\", \"nombreMH\": \"ESPAÑA\"}"));
items.Add(Item(id++, 22, "9449", "Eslovaquia", 68, metadata: "{\"codigoMH\": \"9449\", \"nombreMH\": \"ESLOVAQUIA\"}"));
items.Add(Item(id++, 22, "9450", "Ee Uu", 69, metadata: "{\"codigoMH\": \"9450\", \"nombreMH\": \"EE UU\"}"));
items.Add(Item(id++, 22, "9451", "Eslovenia", 70, metadata: "{\"codigoMH\": \"9451\", \"nombreMH\": \"ESLOVENIA\"}"));
items.Add(Item(id++, 22, "9452", "Wallis Y Futuna", 71, metadata: "{\"codigoMH\": \"9452\", \"nombreMH\": \"WALLIS Y FUTUNA\"}"));
items.Add(Item(id++, 22, "9453", "Etiopia", 72, metadata: "{\"codigoMH\": \"9453\", \"nombreMH\": \"ETIOPIA\"}"));
items.Add(Item(id++, 22, "9454", "Eritrea", 73, metadata: "{\"codigoMH\": \"9454\", \"nombreMH\": \"ERITREA\"}"));
items.Add(Item(id++, 22, "9456", "Fiji-Islas", 74, metadata: "{\"codigoMH\": \"9456\", \"nombreMH\": \"FIJI-ISLAS\"}"));
items.Add(Item(id++, 22, "9457", "Estonia", 75, metadata: "{\"codigoMH\": \"9457\", \"nombreMH\": \"ESTONIA\"}"));
items.Add(Item(id++, 22, "9459", "Filipinas", 76, metadata: "{\"codigoMH\": \"9459\", \"nombreMH\": \"FILIPINAS\"}"));
items.Add(Item(id++, 22, "9462", "Finlandia", 77, metadata: "{\"codigoMH\": \"9462\", \"nombreMH\": \"FINLANDIA\"}"));
items.Add(Item(id++, 22, "9465", "Francia", 78, metadata: "{\"codigoMH\": \"9465\", \"nombreMH\": \"FRANCIA\"}"));
items.Add(Item(id++, 22, "9468", "Gabón", 79, metadata: "{\"codigoMH\": \"9468\", \"nombreMH\": \"GABÓN\"}"));
items.Add(Item(id++, 22, "9471", "Gambia", 80, metadata: "{\"codigoMH\": \"9471\", \"nombreMH\": \"GAMBIA\"}"));
items.Add(Item(id++, 22, "9472", "Georgia", 81, metadata: "{\"codigoMH\": \"9472\", \"nombreMH\": \"GEORGIA\"}"));
items.Add(Item(id++, 22, "9474", "Ghana", 82, metadata: "{\"codigoMH\": \"9474\", \"nombreMH\": \"GHANA\"}"));
items.Add(Item(id++, 22, "9477", "Gibraltar", 83, metadata: "{\"codigoMH\": \"9477\", \"nombreMH\": \"GIBRALTAR\"}"));
items.Add(Item(id++, 22, "9480", "Grecia", 84, metadata: "{\"codigoMH\": \"9480\", \"nombreMH\": \"GRECIA\"}"));
items.Add(Item(id++, 22, "9481", "Grenada", 85, metadata: "{\"codigoMH\": \"9481\", \"nombreMH\": \"GRENADA\"}"));
items.Add(Item(id++, 22, "9482", "Groenlandia", 86, metadata: "{\"codigoMH\": \"9482\", \"nombreMH\": \"GROENLANDIA\"}"));
items.Add(Item(id++, 22, "9483", "Guatemala", 87, metadata: "{\"codigoMH\": \"9483\", \"nombreMH\": \"GUATEMALA\"}"));
items.Add(Item(id++, 22, "9486", "Guinea", 88, metadata: "{\"codigoMH\": \"9486\", \"nombreMH\": \"GUINEA\"}"));
items.Add(Item(id++, 22, "9487", "Guyana", 89, metadata: "{\"codigoMH\": \"9487\", \"nombreMH\": \"GUYANA\"}"));
items.Add(Item(id++, 22, "9489", "Guadalupe", 90, metadata: "{\"codigoMH\": \"9489\", \"nombreMH\": \"GUADALUPE\"}"));
items.Add(Item(id++, 22, "9490", "Guam", 91, metadata: "{\"codigoMH\": \"9490\", \"nombreMH\": \"GUAM\"}"));
items.Add(Item(id++, 22, "9491", "Guayana Francesa", 92, metadata: "{\"codigoMH\": \"9491\", \"nombreMH\": \"GUAYANA FRANCESA\"}"));
items.Add(Item(id++, 22, "9492", "Guernsey", 93, metadata: "{\"codigoMH\": \"9492\", \"nombreMH\": \"GUERNSEY\"}"));
items.Add(Item(id++, 22, "9493", "Guinea Ecuatorial", 94, metadata: "{\"codigoMH\": \"9493\", \"nombreMH\": \"GUINEA ECUATORIAL\"}"));
items.Add(Item(id++, 22, "9494", "Guinea-Bissau", 95, metadata: "{\"codigoMH\": \"9494\", \"nombreMH\": \"GUINEA-BISSAU\"}"));
items.Add(Item(id++, 22, "9495", "Haití", 96, metadata: "{\"codigoMH\": \"9495\", \"nombreMH\": \"HAITÍ\"}"));
items.Add(Item(id++, 22, "9498", "Holanda", 97, metadata: "{\"codigoMH\": \"9498\", \"nombreMH\": \"HOLANDA\"}"));
items.Add(Item(id++, 22, "9501", "Honduras", 98, metadata: "{\"codigoMH\": \"9501\", \"nombreMH\": \"HONDURAS\"}"));
items.Add(Item(id++, 22, "9504", "Hong Kong", 99, metadata: "{\"codigoMH\": \"9504\", \"nombreMH\": \"HONG KONG\"}"));
items.Add(Item(id++, 22, "9507", "Hungría", 100, metadata: "{\"codigoMH\": \"9507\", \"nombreMH\": \"HUNGRÍA\"}"));
items.Add(Item(id++, 22, "9510", "India", 101, metadata: "{\"codigoMH\": \"9510\", \"nombreMH\": \"INDIA\"}"));
items.Add(Item(id++, 22, "9513", "Indonesia", 102, metadata: "{\"codigoMH\": \"9513\", \"nombreMH\": \"INDONESIA\"}"));
items.Add(Item(id++, 22, "9514", "Inglaterra Y Gales", 103, metadata: "{\"codigoMH\": \"9514\", \"nombreMH\": \"INGLATERRA Y GALES\"}"));
items.Add(Item(id++, 22, "9516", "Irak", 104, metadata: "{\"codigoMH\": \"9516\", \"nombreMH\": \"IRAK\"}"));
items.Add(Item(id++, 22, "9519", "Irán", 105, metadata: "{\"codigoMH\": \"9519\", \"nombreMH\": \"IRÁN\"}"));
items.Add(Item(id++, 22, "9521", "Isla De Man", 106, metadata: "{\"codigoMH\": \"9521\", \"nombreMH\": \"ISLA DE MAN\"}"));
items.Add(Item(id++, 22, "9522", "Irlanda", 107, metadata: "{\"codigoMH\": \"9522\", \"nombreMH\": \"IRLANDA\"}"));
items.Add(Item(id++, 22, "9523", "Isla De Navidad", 108, metadata: "{\"codigoMH\": \"9523\", \"nombreMH\": \"ISLA DE NAVIDAD\"}"));
items.Add(Item(id++, 22, "9524", "Isla De Cocos", 109, metadata: "{\"codigoMH\": \"9524\", \"nombreMH\": \"ISLA DE COCOS\"}"));
items.Add(Item(id++, 22, "9525", "Islandia", 110, metadata: "{\"codigoMH\": \"9525\", \"nombreMH\": \"ISLANDIA\"}"));
items.Add(Item(id++, 22, "9526", "Islas Salomón", 111, metadata: "{\"codigoMH\": \"9526\", \"nombreMH\": \"ISLAS SALOMÓN\"}"));
items.Add(Item(id++, 22, "9527", "Islas Cook", 112, metadata: "{\"codigoMH\": \"9527\", \"nombreMH\": \"ISLAS COOK\"}"));
items.Add(Item(id++, 22, "9528", "Israel", 113, metadata: "{\"codigoMH\": \"9528\", \"nombreMH\": \"ISRAEL\"}"));
items.Add(Item(id++, 22, "9529", "Islas Feroe", 114, metadata: "{\"codigoMH\": \"9529\", \"nombreMH\": \"ISLAS FEROE\"}"));
items.Add(Item(id++, 22, "9530", "Islas Azores", 115, metadata: "{\"codigoMH\": \"9530\", \"nombreMH\": \"ISLAS AZORES\"}"));
items.Add(Item(id++, 22, "9531", "Italia", 116, metadata: "{\"codigoMH\": \"9531\", \"nombreMH\": \"ITALIA\"}"));
items.Add(Item(id++, 22, "9532", "Isla Qeshm", 117, metadata: "{\"codigoMH\": \"9532\", \"nombreMH\": \"ISLA QESHM\"}"));
items.Add(Item(id++, 22, "9533", "Islas Malvinas", 118, metadata: "{\"codigoMH\": \"9533\", \"nombreMH\": \"ISLAS MALVINAS\"}"));
items.Add(Item(id++, 22, "9534", "Jamaica", 119, metadata: "{\"codigoMH\": \"9534\", \"nombreMH\": \"JAMAICA\"}"));
items.Add(Item(id++, 22, "9535", "Islas Marianas Del Norte", 120, metadata: "{\"codigoMH\": \"9535\", \"nombreMH\": \"ISLAS MARIANAS DEL NORTE\"}"));
items.Add(Item(id++, 22, "9536", "Islas Marshall", 121, metadata: "{\"codigoMH\": \"9536\", \"nombreMH\": \"ISLAS MARSHALL\"}"));
items.Add(Item(id++, 22, "9537", "Japón", 122, metadata: "{\"codigoMH\": \"9537\", \"nombreMH\": \"JAPÓN\"}"));
items.Add(Item(id++, 22, "9538", "Islas Pitcaim", 123, metadata: "{\"codigoMH\": \"9538\", \"nombreMH\": \"ISLAS PITCAIM\"}"));
items.Add(Item(id++, 22, "9539", "Islas Turcas Y Caicos", 124, metadata: "{\"codigoMH\": \"9539\", \"nombreMH\": \"ISLAS TURCAS Y CAICOS\"}"));
items.Add(Item(id++, 22, "9540", "Jordania", 125, metadata: "{\"codigoMH\": \"9540\", \"nombreMH\": \"JORDANIA\"}"));
items.Add(Item(id++, 22, "9541", "Kasakistan", 126, metadata: "{\"codigoMH\": \"9541\", \"nombreMH\": \"KASAKISTAN\"}"));
items.Add(Item(id++, 22, "9542", "Islas Ultramarinas De Ee Uu", 127, metadata: "{\"codigoMH\": \"9542\", \"nombreMH\": \"ISLAS ULTRAMARINAS DE EE UU\"}"));
items.Add(Item(id++, 22, "9543", "Kenia", 128, metadata: "{\"codigoMH\": \"9543\", \"nombreMH\": \"KENIA\"}"));
items.Add(Item(id++, 22, "9544", "Kiribati", 129, metadata: "{\"codigoMH\": \"9544\", \"nombreMH\": \"KIRIBATI\"}"));
items.Add(Item(id++, 22, "9545", "Islas Vírgenes Estadounidenses", 130, metadata: "{\"codigoMH\": \"9545\", \"nombreMH\": \"ISLAS VÍRGENES ESTADOUNIDENSES\"}"));
items.Add(Item(id++, 22, "9546", "Kuwait", 131, metadata: "{\"codigoMH\": \"9546\", \"nombreMH\": \"KUWAIT\"}"));
items.Add(Item(id++, 22, "9547", "Jersey", 132, metadata: "{\"codigoMH\": \"9547\", \"nombreMH\": \"JERSEY\"}"));
items.Add(Item(id++, 22, "9548", "Kirguistán", 133, metadata: "{\"codigoMH\": \"9548\", \"nombreMH\": \"KIRGUISTÁN\"}"));
items.Add(Item(id++, 22, "9549", "Laos", 134, metadata: "{\"codigoMH\": \"9549\", \"nombreMH\": \"LAOS\"}"));
items.Add(Item(id++, 22, "9551", "Letonia", 135, metadata: "{\"codigoMH\": \"9551\", \"nombreMH\": \"LETONIA\"}"));
items.Add(Item(id++, 22, "9552", "Lesotho", 136, metadata: "{\"codigoMH\": \"9552\", \"nombreMH\": \"LESOTHO\"}"));
items.Add(Item(id++, 22, "9555", "Líbano", 137, metadata: "{\"codigoMH\": \"9555\", \"nombreMH\": \"LÍBANO\"}"));
items.Add(Item(id++, 22, "9558", "Liberia", 138, metadata: "{\"codigoMH\": \"9558\", \"nombreMH\": \"LIBERIA\"}"));
items.Add(Item(id++, 22, "9561", "Libia", 139, metadata: "{\"codigoMH\": \"9561\", \"nombreMH\": \"LIBIA\"}"));
items.Add(Item(id++, 22, "9564", "Liechtenstein", 140, metadata: "{\"codigoMH\": \"9564\", \"nombreMH\": \"LIECHTENSTEIN\"}"));
items.Add(Item(id++, 22, "9565", "Lituania", 141, metadata: "{\"codigoMH\": \"9565\", \"nombreMH\": \"LITUANIA\"}"));
items.Add(Item(id++, 22, "9567", "Luxemburgo", 142, metadata: "{\"codigoMH\": \"9567\", \"nombreMH\": \"LUXEMBURGO\"}"));
items.Add(Item(id++, 22, "9568", "Macao", 143, metadata: "{\"codigoMH\": \"9568\", \"nombreMH\": \"MACAO\"}"));
items.Add(Item(id++, 22, "9570", "Madagascar", 144, metadata: "{\"codigoMH\": \"9570\", \"nombreMH\": \"MADAGASCAR\"}"));
items.Add(Item(id++, 22, "9571", "Macedonia", 145, metadata: "{\"codigoMH\": \"9571\", \"nombreMH\": \"MACEDONIA\"}"));
items.Add(Item(id++, 22, "9573", "Malasia", 146, metadata: "{\"codigoMH\": \"9573\", \"nombreMH\": \"MALASIA\"}"));
items.Add(Item(id++, 22, "9574", "Mali", 147, metadata: "{\"codigoMH\": \"9574\", \"nombreMH\": \"MALI\"}"));
items.Add(Item(id++, 22, "9576", "Malawi", 148, metadata: "{\"codigoMH\": \"9576\", \"nombreMH\": \"MALAWI\"}"));
items.Add(Item(id++, 22, "9577", "Maldivas", 149, metadata: "{\"codigoMH\": \"9577\", \"nombreMH\": \"MALDIVAS\"}"));
items.Add(Item(id++, 22, "9579", "Mali", 150, metadata: "{\"codigoMH\": \"9579\", \"nombreMH\": \"MALI\"}"));
items.Add(Item(id++, 22, "9582", "Malta", 151, metadata: "{\"codigoMH\": \"9582\", \"nombreMH\": \"MALTA\"}"));
items.Add(Item(id++, 22, "9585", "Marruecos", 152, metadata: "{\"codigoMH\": \"9585\", \"nombreMH\": \"MARRUECOS\"}"));
items.Add(Item(id++, 22, "9591", "Mascate Y Omán", 153, metadata: "{\"codigoMH\": \"9591\", \"nombreMH\": \"MASCATE Y OMÁN\"}"));
items.Add(Item(id++, 22, "9594", "Mauricio", 154, metadata: "{\"codigoMH\": \"9594\", \"nombreMH\": \"MAURICIO\"}"));
items.Add(Item(id++, 22, "9597", "Mauritania", 155, metadata: "{\"codigoMH\": \"9597\", \"nombreMH\": \"MAURITANIA\"}"));
items.Add(Item(id++, 22, "9598", "Mayotte", 156, metadata: "{\"codigoMH\": \"9598\", \"nombreMH\": \"MAYOTTE\"}"));
items.Add(Item(id++, 22, "9600", "México", 157, metadata: "{\"codigoMH\": \"9600\", \"nombreMH\": \"MÉXICO\"}"));
items.Add(Item(id++, 22, "9601", "Micronesia", 158, metadata: "{\"codigoMH\": \"9601\", \"nombreMH\": \"MICRONESIA\"}"));
items.Add(Item(id++, 22, "9602", "Moldavia", 159, metadata: "{\"codigoMH\": \"9602\", \"nombreMH\": \"MOLDAVIA\"}"));
items.Add(Item(id++, 22, "9603", "Mónaco", 160, metadata: "{\"codigoMH\": \"9603\", \"nombreMH\": \"MÓNACO\"}"));
items.Add(Item(id++, 22, "9606", "Mongolia", 161, metadata: "{\"codigoMH\": \"9606\", \"nombreMH\": \"MONGOLIA\"}"));
items.Add(Item(id++, 22, "9607", "Montenegro", 162, metadata: "{\"codigoMH\": \"9607\", \"nombreMH\": \"MONTENEGRO\"}"));
items.Add(Item(id++, 22, "9608", "Monserrat", 163, metadata: "{\"codigoMH\": \"9608\", \"nombreMH\": \"MONSERRAT\"}"));
items.Add(Item(id++, 22, "9609", "Mozambique", 164, metadata: "{\"codigoMH\": \"9609\", \"nombreMH\": \"MOZAMBIQUE\"}"));
items.Add(Item(id++, 22, "9610", "Namibia", 165, metadata: "{\"codigoMH\": \"9610\", \"nombreMH\": \"NAMIBIA\"}"));
items.Add(Item(id++, 22, "9611", "Nauru", 166, metadata: "{\"codigoMH\": \"9611\", \"nombreMH\": \"NAURU\"}"));
items.Add(Item(id++, 22, "9612", "Nepal", 167, metadata: "{\"codigoMH\": \"9612\", \"nombreMH\": \"NEPAL\"}"));
items.Add(Item(id++, 22, "9615", "Nicaragua", 168, metadata: "{\"codigoMH\": \"9615\", \"nombreMH\": \"NICARAGUA\"}"));
items.Add(Item(id++, 22, "9618", "Níger", 169, metadata: "{\"codigoMH\": \"9618\", \"nombreMH\": \"NÍGER\"}"));
items.Add(Item(id++, 22, "9621", "Nigeria", 170, metadata: "{\"codigoMH\": \"9621\", \"nombreMH\": \"NIGERIA\"}"));
items.Add(Item(id++, 22, "9622", "Niue", 171, metadata: "{\"codigoMH\": \"9622\", \"nombreMH\": \"NIUE\"}"));
items.Add(Item(id++, 22, "9623", "Norfolk", 172, metadata: "{\"codigoMH\": \"9623\", \"nombreMH\": \"NORFOLK\"}"));
items.Add(Item(id++, 22, "9624", "Noruega", 173, metadata: "{\"codigoMH\": \"9624\", \"nombreMH\": \"NORUEGA\"}"));
items.Add(Item(id++, 22, "9627", "Nva Caledonia", 174, metadata: "{\"codigoMH\": \"9627\", \"nombreMH\": \"NVA CALEDONIA\"}"));
items.Add(Item(id++, 22, "9633", "Nva Zelandia", 175, metadata: "{\"codigoMH\": \"9633\", \"nombreMH\": \"NVA ZELANDIA\"}"));
items.Add(Item(id++, 22, "9636", "Nuevas Hebridas", 176, metadata: "{\"codigoMH\": \"9636\", \"nombreMH\": \"NUEVAS HEBRIDAS\"}"));
items.Add(Item(id++, 22, "9638", "Papua Nv Guinea", 177, metadata: "{\"codigoMH\": \"9638\", \"nombreMH\": \"PAPUA NV GUINEA\"}"));
items.Add(Item(id++, 22, "9639", "Pakistán", 178, metadata: "{\"codigoMH\": \"9639\", \"nombreMH\": \"PAKISTÁN\"}"));
items.Add(Item(id++, 22, "9640", "Palestina", 179, metadata: "{\"codigoMH\": \"9640\", \"nombreMH\": \"PALESTINA\"}"));
items.Add(Item(id++, 22, "9641", "Croacia", 180, metadata: "{\"codigoMH\": \"9641\", \"nombreMH\": \"CROACIA\"}"));
items.Add(Item(id++, 22, "9642", "Panamá", 181, metadata: "{\"codigoMH\": \"9642\", \"nombreMH\": \"PANAMÁ\"}"));
items.Add(Item(id++, 22, "9643", "Palaos", 182, metadata: "{\"codigoMH\": \"9643\", \"nombreMH\": \"PALAOS\"}"));
items.Add(Item(id++, 22, "9645", "Paraguay", 183, metadata: "{\"codigoMH\": \"9645\", \"nombreMH\": \"PARAGUAY\"}"));
items.Add(Item(id++, 22, "9648", "Perú", 184, metadata: "{\"codigoMH\": \"9648\", \"nombreMH\": \"PERÚ\"}"));
items.Add(Item(id++, 22, "9651", "Polonia", 185, metadata: "{\"codigoMH\": \"9651\", \"nombreMH\": \"POLONIA\"}"));
items.Add(Item(id++, 22, "9652", "Polinesia Francesa", 186, metadata: "{\"codigoMH\": \"9652\", \"nombreMH\": \"POLINESIA FRANCESA\"}"));
items.Add(Item(id++, 22, "9654", "Portugal", 187, metadata: "{\"codigoMH\": \"9654\", \"nombreMH\": \"PORTUGAL\"}"));
items.Add(Item(id++, 22, "9660", "Qatar", 188, metadata: "{\"codigoMH\": \"9660\", \"nombreMH\": \"QATAR\"}"));
items.Add(Item(id++, 22, "9663", "El Reino Unido", 189, metadata: "{\"codigoMH\": \"9663\", \"nombreMH\": \"EL REINO UNIDO\"}"));
items.Add(Item(id++, 22, "9664", "Republica Checa", 190, metadata: "{\"codigoMH\": \"9664\", \"nombreMH\": \"REPUBLICA CHECA\"}"));
items.Add(Item(id++, 22, "9666", "Egipto", 191, metadata: "{\"codigoMH\": \"9666\", \"nombreMH\": \"EGIPTO\"}"));
items.Add(Item(id++, 22, "9667", "Reunión", 192, metadata: "{\"codigoMH\": \"9667\", \"nombreMH\": \"REUNIÓN\"}"));
items.Add(Item(id++, 22, "9669", "Rodesia", 193, metadata: "{\"codigoMH\": \"9669\", \"nombreMH\": \"RODESIA\"}"));
items.Add(Item(id++, 22, "9672", "Ruanda", 194, metadata: "{\"codigoMH\": \"9672\", \"nombreMH\": \"RUANDA\"}"));
items.Add(Item(id++, 22, "9673", "Republica De Armenia", 195, metadata: "{\"codigoMH\": \"9673\", \"nombreMH\": \"REPUBLICA DE ARMENIA\"}"));
items.Add(Item(id++, 22, "9675", "Rumania", 196, metadata: "{\"codigoMH\": \"9675\", \"nombreMH\": \"RUMANIA\"}"));
items.Add(Item(id++, 22, "9676", "Sahara Occidental", 197, metadata: "{\"codigoMH\": \"9676\", \"nombreMH\": \"SAHARA OCCIDENTAL\"}"));
items.Add(Item(id++, 22, "9677", "San Marino", 198, metadata: "{\"codigoMH\": \"9677\", \"nombreMH\": \"SAN MARINO\"}"));
items.Add(Item(id++, 22, "9678", "Samoa Occid", 199, metadata: "{\"codigoMH\": \"9678\", \"nombreMH\": \"SAMOA OCCID\"}"));
items.Add(Item(id++, 22, "9679", "Saint Kitts And Nevis", 200, metadata: "{\"codigoMH\": \"9679\", \"nombreMH\": \"SAINT KITTS AND NEVIS\"}"));
items.Add(Item(id++, 22, "9680", "Santa Lucia", 201, metadata: "{\"codigoMH\": \"9680\", \"nombreMH\": \"SANTA LUCIA\"}"));
items.Add(Item(id++, 22, "9681", "Senegal", 202, metadata: "{\"codigoMH\": \"9681\", \"nombreMH\": \"SENEGAL\"}"));
items.Add(Item(id++, 22, "9682", "Saotome Y Princ", 203, metadata: "{\"codigoMH\": \"9682\", \"nombreMH\": \"SAOTOME Y PRINC\"}"));
items.Add(Item(id++, 22, "9683", "Sn Vic Y Grenad", 204, metadata: "{\"codigoMH\": \"9683\", \"nombreMH\": \"SN VIC Y GRENAD\"}"));
items.Add(Item(id++, 22, "9684", "Sierra Leona", 205, metadata: "{\"codigoMH\": \"9684\", \"nombreMH\": \"SIERRA LEONA\"}"));
items.Add(Item(id++, 22, "9685", "Samoa Americana", 206, metadata: "{\"codigoMH\": \"9685\", \"nombreMH\": \"SAMOA AMERICANA\"}"));
items.Add(Item(id++, 22, "9686", "San Pedro Y Miquelón", 207, metadata: "{\"codigoMH\": \"9686\", \"nombreMH\": \"SAN PEDRO Y MIQUELÓN\"}"));
items.Add(Item(id++, 22, "9687", "Singapur", 208, metadata: "{\"codigoMH\": \"9687\", \"nombreMH\": \"SINGAPUR\"}"));
items.Add(Item(id++, 22, "9688", "Santa Elena", 209, metadata: "{\"codigoMH\": \"9688\", \"nombreMH\": \"SANTA ELENA\"}"));
items.Add(Item(id++, 22, "9689", "Serbia", 210, metadata: "{\"codigoMH\": \"9689\", \"nombreMH\": \"SERBIA\"}"));
items.Add(Item(id++, 22, "9690", "Siria", 211, metadata: "{\"codigoMH\": \"9690\", \"nombreMH\": \"SIRIA\"}"));
items.Add(Item(id++, 22, "9691", "Seychelles", 212, metadata: "{\"codigoMH\": \"9691\", \"nombreMH\": \"SEYCHELLES\"}"));
items.Add(Item(id++, 22, "9692", "Svalbard Y Jan Mayen", 213, metadata: "{\"codigoMH\": \"9692\", \"nombreMH\": \"SVALBARD Y JAN MAYEN\"}"));
items.Add(Item(id++, 22, "9693", "Somalia", 214, metadata: "{\"codigoMH\": \"9693\", \"nombreMH\": \"SOMALIA\"}"));
items.Add(Item(id++, 22, "9696", "Sudáfrica Rep", 215, metadata: "{\"codigoMH\": \"9696\", \"nombreMH\": \"SUDÁFRICA REP\"}"));
items.Add(Item(id++, 22, "9699", "Sudan", 216, metadata: "{\"codigoMH\": \"9699\", \"nombreMH\": \"SUDAN\"}"));
items.Add(Item(id++, 22, "9702", "Suecia", 217, metadata: "{\"codigoMH\": \"9702\", \"nombreMH\": \"SUECIA\"}"));
items.Add(Item(id++, 22, "9705", "Suiza", 218, metadata: "{\"codigoMH\": \"9705\", \"nombreMH\": \"SUIZA\"}"));
items.Add(Item(id++, 22, "9706", "Surinam", 219, metadata: "{\"codigoMH\": \"9706\", \"nombreMH\": \"SURINAM\"}"));
items.Add(Item(id++, 22, "9707", "Sri Lanka", 220, metadata: "{\"codigoMH\": \"9707\", \"nombreMH\": \"SRI LANKA\"}"));
items.Add(Item(id++, 22, "9708", "Suecilandia", 221, metadata: "{\"codigoMH\": \"9708\", \"nombreMH\": \"SUECILANDIA\"}"));
items.Add(Item(id++, 22, "9709", "Tayikistán", 222, metadata: "{\"codigoMH\": \"9709\", \"nombreMH\": \"TAYIKISTÁN\"}"));
items.Add(Item(id++, 22, "9711", "Tailandia", 223, metadata: "{\"codigoMH\": \"9711\", \"nombreMH\": \"TAILANDIA\"}"));
items.Add(Item(id++, 22, "9712", "Territorio Británico Del Océano Indico", 224, metadata: "{\"codigoMH\": \"9712\", \"nombreMH\": \"TERRITORIO BRITÁNICO DEL OCÉANO INDICO\"}"));
items.Add(Item(id++, 22, "9713", "Territorios Australes Franceses", 225, metadata: "{\"codigoMH\": \"9713\", \"nombreMH\": \"TERRITORIOS AUSTRALES FRANCESES\"}"));
items.Add(Item(id++, 22, "9714", "Tanzania", 226, metadata: "{\"codigoMH\": \"9714\", \"nombreMH\": \"TANZANIA\"}"));
items.Add(Item(id++, 22, "9715", "Territorios Palestinos", 227, metadata: "{\"codigoMH\": \"9715\", \"nombreMH\": \"TERRITORIOS PALESTINOS\"}"));
items.Add(Item(id++, 22, "9716", "Timor Oriental", 228, metadata: "{\"codigoMH\": \"9716\", \"nombreMH\": \"TIMOR ORIENTAL\"}"));
items.Add(Item(id++, 22, "9717", "Togo", 229, metadata: "{\"codigoMH\": \"9717\", \"nombreMH\": \"TOGO\"}"));
items.Add(Item(id++, 22, "9718", "Tokelau", 230, metadata: "{\"codigoMH\": \"9718\", \"nombreMH\": \"TOKELAU\"}"));
items.Add(Item(id++, 22, "9719", "Turkmenistán", 231, metadata: "{\"codigoMH\": \"9719\", \"nombreMH\": \"TURKMENISTÁN\"}"));
items.Add(Item(id++, 22, "9720", "Trinidad Tobago", 232, metadata: "{\"codigoMH\": \"9720\", \"nombreMH\": \"TRINIDAD TOBAGO\"}"));
items.Add(Item(id++, 22, "9722", "Tonga", 233, metadata: "{\"codigoMH\": \"9722\", \"nombreMH\": \"TONGA\"}"));
items.Add(Item(id++, 22, "9723", "Túnez", 234, metadata: "{\"codigoMH\": \"9723\", \"nombreMH\": \"TÚNEZ\"}"));
items.Add(Item(id++, 22, "9725", "Transkei", 235, metadata: "{\"codigoMH\": \"9725\", \"nombreMH\": \"TRANSKEI\"}"));
items.Add(Item(id++, 22, "9726", "Turquía", 236, metadata: "{\"codigoMH\": \"9726\", \"nombreMH\": \"TURQUÍA\"}"));
items.Add(Item(id++, 22, "9727", "Tuvalu", 237, metadata: "{\"codigoMH\": \"9727\", \"nombreMH\": \"TUVALU\"}"));
items.Add(Item(id++, 22, "9729", "Uganda", 238, metadata: "{\"codigoMH\": \"9729\", \"nombreMH\": \"UGANDA\"}"));
items.Add(Item(id++, 22, "9732", "Urss", 239, metadata: "{\"codigoMH\": \"9732\", \"nombreMH\": \"URSS\"}"));
items.Add(Item(id++, 22, "9733", "Rusia", 240, metadata: "{\"codigoMH\": \"9733\", \"nombreMH\": \"RUSIA\"}"));
items.Add(Item(id++, 22, "9735", "Uruguay", 241, metadata: "{\"codigoMH\": \"9735\", \"nombreMH\": \"URUGUAY\"}"));
items.Add(Item(id++, 22, "9736", "Ucrania", 242, metadata: "{\"codigoMH\": \"9736\", \"nombreMH\": \"UCRANIA\"}"));
items.Add(Item(id++, 22, "9737", "Uzbekistán", 243, metadata: "{\"codigoMH\": \"9737\", \"nombreMH\": \"UZBEKISTÁN\"}"));
items.Add(Item(id++, 22, "9738", "Vaticano", 244, metadata: "{\"codigoMH\": \"9738\", \"nombreMH\": \"VATICANO\"}"));
items.Add(Item(id++, 22, "9739", "Vanuatu", 245, metadata: "{\"codigoMH\": \"9739\", \"nombreMH\": \"VANUATU\"}"));
items.Add(Item(id++, 22, "9740", "Venda", 246, metadata: "{\"codigoMH\": \"9740\", \"nombreMH\": \"VENDA\"}"));
items.Add(Item(id++, 22, "9741", "Venezuela", 247, metadata: "{\"codigoMH\": \"9741\", \"nombreMH\": \"VENEZUELA\"}"));
items.Add(Item(id++, 22, "9744", "Vietnam Norte", 248, metadata: "{\"codigoMH\": \"9744\", \"nombreMH\": \"VIETNAM NORTE\"}"));
items.Add(Item(id++, 22, "9746", "Vietnam", 249, metadata: "{\"codigoMH\": \"9746\", \"nombreMH\": \"VIETNAM\"}"));
items.Add(Item(id++, 22, "9747", "Vietnam Sur", 250, metadata: "{\"codigoMH\": \"9747\", \"nombreMH\": \"VIETNAM SUR\"}"));
items.Add(Item(id++, 22, "9750", "Yemen Sur", 251, metadata: "{\"codigoMH\": \"9750\", \"nombreMH\": \"YEMEN SUR\"}"));
items.Add(Item(id++, 22, "9751", "Yibuti", 252, metadata: "{\"codigoMH\": \"9751\", \"nombreMH\": \"YIBUTI\"}"));
items.Add(Item(id++, 22, "9756", "Rep Yugoslavia", 253, metadata: "{\"codigoMH\": \"9756\", \"nombreMH\": \"REP YUGOSLAVIA\"}"));
items.Add(Item(id++, 22, "9758", "Zaire", 254, metadata: "{\"codigoMH\": \"9758\", \"nombreMH\": \"ZAIRE\"}"));
items.Add(Item(id++, 22, "9759", "Zambia", 255, metadata: "{\"codigoMH\": \"9759\", \"nombreMH\": \"ZAMBIA\"}"));
items.Add(Item(id++, 22, "9760", "Zimbabwe", 256, metadata: "{\"codigoMH\": \"9760\", \"nombreMH\": \"ZIMBABWE\"}"));
items.Add(Item(id++, 22, "9850", "Puerto Rico", 257, metadata: "{\"codigoMH\": \"9850\", \"nombreMH\": \"PUERTO RICO\"}"));
items.Add(Item(id++, 22, "9862", "Bahamas", 258, metadata: "{\"codigoMH\": \"9862\", \"nombreMH\": \"BAHAMAS\"}"));
items.Add(Item(id++, 22, "9863", "Bermudas", 259, metadata: "{\"codigoMH\": \"9863\", \"nombreMH\": \"BERMUDAS\"}"));
items.Add(Item(id++, 22, "9865", "Martinica", 260, metadata: "{\"codigoMH\": \"9865\", \"nombreMH\": \"MARTINICA\"}"));
items.Add(Item(id++, 22, "9886", "Nueva Guinea", 261, metadata: "{\"codigoMH\": \"9886\", \"nombreMH\": \"NUEVA GUINEA\"}"));
items.Add(Item(id++, 22, "9887", "Islas Gran Caimán", 262, metadata: "{\"codigoMH\": \"9887\", \"nombreMH\": \"ISLAS GRAN CAIMÁN\"}"));
items.Add(Item(id++, 22, "9888", "San Maarten", 263, metadata: "{\"codigoMH\": \"9888\", \"nombreMH\": \"SAN MAARTEN\"}"));
items.Add(Item(id++, 22, "9897", "Islas Vírgenes Británicas", 264, metadata: "{\"codigoMH\": \"9897\", \"nombreMH\": \"ISLAS VÍRGENES BRITÁNICAS\"}"));
items.Add(Item(id++, 22, "9898", "Ant Holandesas", 265, metadata: "{\"codigoMH\": \"9898\", \"nombreMH\": \"ANT HOLANDESAS\"}"));
items.Add(Item(id++, 22, "9899", "Taiwán", 266, metadata: "{\"codigoMH\": \"9899\", \"nombreMH\": \"TAIWÁN\"}"));
items.Add(Item(id++, 22, "9900", "Delaware (Usa)", 267, metadata: "{\"codigoMH\": \"9900\", \"nombreMH\": \"DELAWARE (USA)\"}"));
items.Add(Item(id++, 22, "9901", "Nevada (Usa)", 268, metadata: "{\"codigoMH\": \"9901\", \"nombreMH\": \"NEVADA (USA)\"}"));
items.Add(Item(id++, 22, "9902", "Wyoming (Usa)", 269, metadata: "{\"codigoMH\": \"9902\", \"nombreMH\": \"WYOMING (USA)\"}"));
items.Add(Item(id++, 22, "9903", "Campione D'Italia, Italia", 270, metadata: "{\"codigoMH\": \"9903\", \"nombreMH\": \"CAMPIONE D'ITALIA, ITALIA\"}"));
items.Add(Item(id++, 22, "9904", "Florida (Usa)", 271, metadata: "{\"codigoMH\": \"9904\", \"nombreMH\": \"FLORIDA (USA)\"}"));
items.Add(Item(id++, 22, "9905", "Dakota Del Sur (Usa)", 272, metadata: "{\"codigoMH\": \"9905\", \"nombreMH\": \"DAKOTA DEL SUR (USA)\"}"));
items.Add(Item(id++, 22, "9906", "Texas (Usa)", 273, metadata: "{\"codigoMH\": \"9906\", \"nombreMH\": \"TEXAS (USA)\"}"));
items.Add(Item(id++, 22, "9907", "Washington (Usa)", 274, metadata: "{\"codigoMH\": \"9907\", \"nombreMH\": \"WASHINGTON (USA)\"}"));
items.Add(Item(id++, 22, "9999", "No definido en migración", 275, metadata: "{\"codigoMH\": \"9999\", \"nombreMH\": \"No definido en migración\"}"));

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
