using Identity.Application.Common;

namespace Identity.Application.Branches;

/// <summary>
/// Contrato de serviço do CRUD de filiais. Todas as operações recebem o
/// TenantId de forma explícita: a query SEMPRE filtra por ele, garantindo que
/// nenhuma filial de outro tenant seja criada, lida, editada ou listada.
/// </summary>
public interface IBranchService
{
    /// <summary>Cria uma filial. Retorna null se o tenant não existir ou estiver
    /// inativo (soft-deletado).</summary>
    Task<BranchDto?> CreateAsync(CreateBranchCommand command, CancellationToken ct = default);

    /// <summary>Edita uma filial do tenant. Retorna null se não existir/inativa.</summary>
    Task<BranchDto?> UpdateAsync(UpdateBranchCommand command, CancellationToken ct = default);

    /// <summary>Soft delete de uma filial do tenant. Retorna false se não existir.</summary>
    Task<bool> SoftDeleteAsync(SoftDeleteBranchCommand command, CancellationToken ct = default);

    /// <summary>Lista paginada das filiais do tenant (nunca de outro tenant).</summary>
    Task<PagedResult<BranchDto>> ListByTenantAsync(ListBranchesByTenantQuery query, CancellationToken ct = default);

    /// <summary>Detalhe de uma filial do tenant. Retorna null se não existir.</summary>
    Task<BranchDto?> GetByIdAsync(GetBranchByIdQuery query, CancellationToken ct = default);
}
