namespace NeoSTP.Application.Catalogos.Dtos;

public class CatalogoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; }
    public int? EmpresaId { get; set; }
}

public class CatalogoItemDto
{
    public int Id { get; set; }
    public int CatalogoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Valor { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
    public string? MetadataJson { get; set; }
}
