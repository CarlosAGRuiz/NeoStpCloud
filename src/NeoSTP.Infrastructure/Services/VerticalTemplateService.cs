using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Onboarding;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class VerticalTemplateService : IVerticalTemplateService
{
    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public VerticalTemplateService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static readonly VerticalTemplateDto[] Plantillas =
    {
        new()
        {
            Codigo = "FARMACIA", Nombre = "Farmacia", Icono = "medication",
            Descripcion = "Medicamentos y cuidado de la salud. Recuerda marcar \"controla lote\" en los productos con vencimiento.",
            Categorias = ["MEDICAMENTOS", "SALUD", "HIGIENE", "CUIDADO_PERSONAL", "VITAMINAS", "BEBES", "DERMOCOSMETICA"],
        },
        new()
        {
            Codigo = "FERRETERIA", Nombre = "Ferretería", Icono = "hardware",
            Descripcion = "Herramientas y materiales de construcción.",
            Categorias = ["HERRAMIENTAS", "ELECTRICO", "FONTANERIA", "PINTURAS", "CONSTRUCCION", "JARDINERIA", "SEGURIDAD"],
        },
        new()
        {
            Codigo = "SALON", Nombre = "Salón de belleza", Icono = "content_cut",
            Descripcion = "Servicios de belleza y productos para la venta.",
            Categorias = ["CORTE", "COLOR", "TRATAMIENTOS", "UNAS", "MAQUILLAJE", "PRODUCTOS_VENTA"],
        },
        new()
        {
            Codigo = "TIENDA", Nombre = "Tienda / Minimarket", Icono = "storefront",
            Descripcion = "Abarrotes y consumo diario.",
            Categorias = ["ABARROTES", "BEBIDAS", "LACTEOS", "SNACKS", "LIMPIEZA", "CUIDADO_PERSONAL"],
        },
        new()
        {
            Codigo = "GENERAL", Nombre = "General / Servicios", Icono = "business_center",
            Descripcion = "Punto de partida neutro para cualquier rubro.",
            Categorias = ["PRODUCTOS", "SERVICIOS"],
        },
    };

    public IReadOnlyList<VerticalTemplateDto> Listar() => Plantillas;

    public async Task<Result<AplicarPlantillaResultDto>> AplicarAsync(int empresaId, string codigo, string? actor, CancellationToken ct = default)
    {
        var plantilla = Plantillas.FirstOrDefault(p =>
            string.Equals(p.Codigo, codigo?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (plantilla is null)
            return Result<AplicarPlantillaResultDto>.Fail($"Plantilla '{codigo}' no existe.", "PLANTILLA_NOT_FOUND");

        var catalogo = await _db.Catalogos.FirstOrDefaultAsync(
            c => c.Codigo == CatalogCodes.CategoriaProducto && c.EmpresaId == empresaId, ct);
        if (catalogo is null)
        {
            catalogo = new Catalogo
            {
                Codigo = CatalogCodes.CategoriaProducto,
                Nombre = "Categorías de producto",
                Descripcion = $"Categorías sembradas por la plantilla {plantilla.Nombre}.",
                EsSistema = false, Activo = true, EmpresaId = empresaId,
                CreatedAt = DateTime.UtcNow, CreatedBy = actor,
            };
            _db.Catalogos.Add(catalogo);
            await _db.SaveChangesAsync(ct);
        }

        var existentes = await _db.CatalogoItems
            .Where(i => i.CatalogoId == catalogo.Id)
            .Select(i => i.Codigo)
            .ToListAsync(ct);
        var set = new HashSet<string>(existentes, StringComparer.OrdinalIgnoreCase);

        var creadas = 0;
        var orden = existentes.Count;
        foreach (var cat in plantilla.Categorias)
        {
            if (!set.Add(cat)) continue;
            _db.CatalogoItems.Add(new CatalogoItem
            {
                CatalogoId = catalogo.Id,
                Codigo = cat,
                Valor = Titulo(cat),
                Orden = ++orden,
                EsSistema = false, Activo = true,
                CreatedAt = DateTime.UtcNow, CreatedBy = actor,
            });
            creadas++;
        }
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = "ONBOARDING",
            Accion = "PLANTILLA_VERTICAL", Entidad = "Catalogo", EntidadId = catalogo.Id.ToString(),
            Resultado = "OK", Detalle = $"Plantilla {plantilla.Codigo}: {creadas} categorías nuevas",
        });

        return Result<AplicarPlantillaResultDto>.Ok(new AplicarPlantillaResultDto
        {
            Codigo = plantilla.Codigo,
            CategoriasCreadas = creadas,
            CategoriasExistentes = plantilla.Categorias.Count - creadas,
        });
    }

    /// <summary>"CUIDADO_PERSONAL" → "Cuidado personal".</summary>
    private static string Titulo(string codigo)
    {
        var texto = codigo.Replace('_', ' ').ToLowerInvariant();
        return char.ToUpperInvariant(texto[0]) + texto[1..];
    }
}
