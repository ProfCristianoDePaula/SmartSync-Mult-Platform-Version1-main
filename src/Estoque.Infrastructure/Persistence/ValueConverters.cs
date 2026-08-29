using Estoque.Domain.Common;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Estoque.Infrastructure.Persistence;

/// <summary>
/// Conversores de valor centralizados. Para propriedades NULLABLE, o EF Core
/// envolve automaticamente o conversor (null passa direto) — desde que o
/// conversor seja declarado entre os tipos NÃO anuláveis.
/// </summary>
public static class ValueConverters
{
    public static readonly ValueConverter<TenantId, Guid> Tenant =
        new(v => v.Value, v => TenantId.From(v));

    public static readonly ValueConverter<BranchId, Guid> Branch =
        new(v => v.Value, v => BranchId.From(v));

    public static readonly ValueConverter<ProductId, Guid> Product =
        new(v => v.Value, v => ProductId.From(v));

    public static readonly ValueConverter<BrandId, Guid> Brand =
        new(v => v.Value, v => BrandId.From(v));

    public static readonly ValueConverter<ModelId, Guid> Model =
        new(v => v.Value, v => ModelId.From(v));

    public static readonly ValueConverter<CategoryId, Guid> Category =
        new(v => v.Value, v => CategoryId.From(v));

    public static readonly ValueConverter<SupplierId, Guid> Supplier =
        new(v => v.Value, v => SupplierId.From(v));

    public static readonly ValueConverter<LotId, Guid> Lot =
        new(v => v.Value, v => LotId.From(v));

    public static readonly ValueConverter<StockRuleId, Guid> StockRule =
        new(v => v.Value, v => StockRuleId.From(v));

    public static readonly ValueConverter<StockMovementId, Guid> StockMovement =
        new(v => v.Value, v => StockMovementId.From(v));

    public static readonly ValueConverter<Sku, string> SkuText =
        new(v => v.Value, v => Sku.FromValidated(v));

    public static readonly ValueConverter<Barcode, string> BarcodeText =
        new(v => v.Value, v => Barcode.FromValidated(v));

    public static readonly ValueConverter<Quantity, decimal> QuantityNumber =
        new(v => v.Value, v => Quantity.FromValidated(v));

    public static readonly ValueConverter<Money, decimal> MoneyNumber =
        new(v => v.Amount, v => Money.FromValidated(v));
}
