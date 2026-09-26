using Fiscal.Application.Auditing;
using Fiscal.Application.Emitentes;
using Fiscal.Application.IntegrationServices;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;

namespace Fiscal.Infrastructure.Emitentes;

public sealed class ConcessaoService(
    IConcessaoRepository concessoes,
    IFilialAccessChecker filiais,
    FiscalDbContext db,
    IAuditLogger audit) : IConcessaoService
{
    public async Task<ConcessaoDto> GrantAsync(GrantConcessaoCommand command, Guid grantedBy, CancellationToken ct = default)
    {
        if (command.UserId == Guid.Empty)
            throw new BusinessRuleViolationException("Usuário obrigatório.");
        if (command.BranchId == Guid.Empty)
            throw new BusinessRuleViolationException("Filial obrigatória.");

        var branchId = BranchId.From(command.BranchId);
        if (await filiais.ValidateAsync(command.TenantId, branchId.Value, ct) != FilialAccess.Allowed)
            throw new BusinessRuleViolationException("Filial não pertence a este tenant.");

        var existente = await concessoes.FindAsync(command.TenantId, branchId, command.UserId, command.Papel, ct);
        if (existente is not null)
            return ToDto(existente);

        var concessao = ConcessaoUnidade.Create(command.TenantId, branchId, command.UserId, command.Papel);
        await concessoes.AddAsync(concessao, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(command.TenantId, grantedBy, "fiscal.concessao.concedida", "ConcessaoUnidade",
            concessao.Id.Value.ToString(), ct);

        return ToDto(concessao);
    }

    public async Task<bool> RevokeAsync(TenantId tenantId, Guid concessaoId, CancellationToken ct = default)
    {
        var concessao = await concessoes.GetAsync(tenantId, concessaoId, ct);
        if (concessao is null) return false;

        concessao.Revogar();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ConcessaoDto>> ListAsync(TenantId tenantId, Guid? branchId = null, CancellationToken ct = default)
    {
        var items = await concessoes.ListAsync(tenantId,
            branchId is null ? null : BranchId.From(branchId.Value), ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<bool> TemAcessoAsync(TenantId tenantId, BranchId branchId, Guid userId, PapelUnidade papel, CancellationToken ct = default)
        => await concessoes.FindAsync(tenantId, branchId, userId, papel, ct) is not null;

    private static ConcessaoDto ToDto(ConcessaoUnidade c) => new(
        c.Id.Value, c.BranchId.Value, c.UserId, c.Papel);
}
