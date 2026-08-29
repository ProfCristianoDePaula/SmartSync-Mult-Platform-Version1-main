using Estoque.Application.Common;
using Estoque.Application.Integracao;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Application.IntegrationServices;

namespace Estoque.Infrastructure.Services.Integracao;

public sealed class XmlImportService(
    IXmlImportRepository imports,
    IFilialAccessChecker filialChecker,
    IUnitOfWork uow) : IXmlImportService
{
    public async Task<XmlImportDto> EnqueueAsync(TenantId tenantId, Guid branchId, Guid? supplierId,
        string fileName, byte[] content, Guid requestedByUserId, CancellationToken ct = default)
    {
        // Validação de filial na recepção (Denied bloqueia; fallback Unknown v1).
        var access = await filialChecker.ValidateAsync(tenantId, branchId, ct);
        if (access == Application.IntegrationServices.FilialAccess.Denied)
            throw new BusinessRuleViolationException("Filial não pertence ao seu tenant.", "branch-denied");

        var import = XmlImport.Enqueue(tenantId, branchId, supplierId, fileName, content, requestedByUserId);
        await imports.AddAsync(import, ct);
        await uow.SaveChangesAsync(ct);

        return ToDto(import);
    }

    public async Task<XmlImportDto?> GetByIdAsync(TenantId tenantId, Guid importId, CancellationToken ct = default)
    {
        var import = await imports.GetAsync(tenantId, importId, ct);
        return import is null ? null : ToDto(import);
    }

    internal static XmlImportDto ToDto(XmlImport i) => new(
        i.Id.Value, i.BranchId.Value, i.SupplierId?.Value, i.FileName, i.Status,
        i.TotalItems, i.ProcessedItems, i.SkippedItems, i.ErrorMessage,
        i.CreatedAtUtc, i.CompletedAtUtc);
}
