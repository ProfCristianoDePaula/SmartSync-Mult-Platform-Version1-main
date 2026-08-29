namespace Estoque.Domain.Enums;

/// <summary>
/// Unidade de medida de um produto.
/// </summary>
public enum UnitOfMeasure
{
    Unidade = 1,
    Quilograma = 2,
    Grama = 3,
    Litro = 4,
    Mililitro = 5,
    Caixa = 6,
    Metro = 7
}

/// <summary>Conversões básicas entre unidades da mesma família (massa/volume).</summary>
public static class UnitOfMeasureExtensions
{
    public static bool IsMass(this UnitOfMeasure uom)
        => uom is UnitOfMeasure.Quilograma or UnitOfMeasure.Grama;

    public static bool IsVolume(this UnitOfMeasure uom)
        => uom is UnitOfMeasure.Litro or UnitOfMeasure.Mililitro;

    public static decimal ToBaseFactor(this UnitOfMeasure uom) => uom switch
    {
        UnitOfMeasure.Grama => 0.001m,      // base: kg
        UnitOfMeasure.Quilograma => 1m,
        UnitOfMeasure.Mililitro => 0.001m,  // base: litro
        UnitOfMeasure.Litro => 1m,
        _ => 1m
    };
}
