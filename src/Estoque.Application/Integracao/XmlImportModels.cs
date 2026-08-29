using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using FluentValidation;

namespace Estoque.Application.Integracao;

public sealed record XmlImportDto(
    Guid Id,
    Guid BranchId,
    Guid? SupplierId,
    string FileName,
    XmlImportStatus Status,
    int TotalItems,
    int ProcessedItems,
    int SkippedItems,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

public interface IXmlImportService
{
    /// <summary>Recebe o XML (multipart), persiste como Recebida e agenda o processamento.</summary>
    Task<XmlImportDto> EnqueueAsync(TenantId tenantId, Guid branchId, Guid? supplierId,
        string fileName, byte[] content, Guid requestedByUserId, CancellationToken ct = default);
    Task<XmlImportDto?> GetByIdAsync(TenantId tenantId, Guid importId, CancellationToken ct = default);
}
