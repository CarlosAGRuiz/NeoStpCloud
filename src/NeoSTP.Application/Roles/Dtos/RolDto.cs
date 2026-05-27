namespace NeoSTP.Application.Roles.Dtos;

public class RolDto
{
    public int Id { get; set; }
    public int? EmpresaId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; }
    public IReadOnlyList<int> PermisoIds { get; set; } = Array.Empty<int>();
    public IReadOnlyList<string> PermisoCodigos { get; set; } = Array.Empty<string>();
}

public class CreateRolRequest
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public IReadOnlyList<int>? PermisoIds { get; set; }
}

public class UpdateRolRequest
{
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public IReadOnlyList<int>? PermisoIds { get; set; }
}

public class PermisoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Modulo { get; set; } = null!;
    public string? Descripcion { get; set; }
}
