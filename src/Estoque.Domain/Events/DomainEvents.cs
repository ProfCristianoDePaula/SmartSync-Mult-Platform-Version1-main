using Estoque.Domain.Common;
using Estoque.Domain.Enums;

namespace Estoque.Domain.Events;

/// <summary>
/// Eventos de domínio do Estoque — records imutáveis coletados nos agregados
/// e despachados PÓS-COMMIT (padrão aprovado na Etapa 23). Consumidores
/// internos geram alertas; consumidores externos leem a outbox.
/// </summary>
public sealed record ProductCreated(ProductId ProductId, TenantId TenantId, string Sku);

public sealed record ProductSoftDeleted(ProductId ProductId, TenantId TenantId);

public sealed record StockMovementRegistered(
    Guid StockMovementId,
    Guid TenantId,
    Guid BranchId,
    Guid ProductId,
    MovementType Type,
    decimal Quantity);

public sealed record LowStockDetected(
    Guid TenantId,
    Guid BranchId,
    Guid ProductId,
    decimal Quantity,
    decimal MinimumQty);

public sealed record LotNearExpiry(
    LotId LotId,
    TenantId TenantId,
    Guid BranchId,
    ProductId ProductId,
    DateOnly ExpiresOn);

public sealed record OutletMarked(
    OutletItemId OutletItemId,
    Guid TenantId,
    Guid BranchId,
    Guid ProductId,
    string Reason);

public sealed record PurchaseSuggestionCreated(
    PurchaseSuggestionId PurchaseSuggestionId,
    Guid TenantId,
    Guid ProductId);

/// <summary>Venda finalizada (Fiscal-13): gatilho de emissão fiscal opcional.</summary>
public sealed record VendaFinalizada(
    Guid VendaId,
    Guid PedidoId,
    Guid TenantId,
    Guid? UnidadeId,
    decimal ValorFinal);
