using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Concessão de usuário interno sobre uma filial (TenantAdmin configura;
/// Manager/Seller operam só com concessão; sem ela, somente leitura).
/// Client nunca recebe concessão.
/// </summary>
public sealed class ConcessaoUnidade : Entity<ConcessaoUnidadeId>
{
    public TenantId TenantId { get; private set; }
    public BranchId BranchId { get; private set; }
    public Guid UserId { get; private set; }
    public PapelUnidade Papel { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private ConcessaoUnidade() { }

    private ConcessaoUnidade(
        ConcessaoUnidadeId id, TenantId tenantId, BranchId branchId, Guid userId, PapelUnidade papel)
        : base(id)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Usuário obrigatório.", nameof(userId));
        TenantId = tenantId;
        BranchId = branchId;
        UserId = userId;
        Papel = papel;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static ConcessaoUnidade Create(TenantId tenantId, BranchId branchId, Guid userId, PapelUnidade papel)
        => new(ConcessaoUnidadeId.New(), tenantId, branchId, userId, papel);

    public void Revogar()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }
}
