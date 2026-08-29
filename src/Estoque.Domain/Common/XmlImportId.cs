namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Importação XML.</summary>
public readonly record struct XmlImportId(Guid Value)
{
    public static XmlImportId New() => new(Guid.NewGuid());
    public static XmlImportId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
