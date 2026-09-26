namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — EventoFiscal.</summary>
public readonly record struct EventoFiscalId(Guid Value)
{
    public static EventoFiscalId New() => new(Guid.NewGuid());
    public static EventoFiscalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
