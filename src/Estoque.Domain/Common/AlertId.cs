namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Alerta.</summary>
public readonly record struct AlertId(Guid Value)
{
    public static AlertId New() => new(Guid.NewGuid());
    public static AlertId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
