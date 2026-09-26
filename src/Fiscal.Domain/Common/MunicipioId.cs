namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — Municipio.</summary>
public readonly record struct MunicipioId(Guid Value)
{
    public static MunicipioId New() => new(Guid.NewGuid());
    public static MunicipioId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
