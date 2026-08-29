namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Sugestão de auto-compra.</summary>
public readonly record struct PurchaseSuggestionId(Guid Value)
{
    public static PurchaseSuggestionId New() => new(Guid.NewGuid());
    public static PurchaseSuggestionId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
