using Estoque.Domain.Common;
using Estoque.Domain.Enums;

namespace Estoque.Domain.Entities;

/// <summary>
/// Importação de XML (NF-e) — máquina de estados:
/// Recebida → Processando → Concluida | Erro.
/// O processamento gera movimentações de ENTRADA em lote.
/// </summary>
public sealed class XmlImport : Entity<XmlImportId>
{
    public TenantId TenantId { get; private set; }
    public BranchId BranchId { get; private set; }
    public SupplierId? SupplierId { get; private set; }

    public string FileName { get; private set; } = null!;
    public byte[] Content { get; private set; } = null!;
    public XmlImportStatus Status { get; private set; }

    public int TotalItems { get; private set; }
    public int ProcessedItems { get; private set; }
    /// <summary>Itens ignorados com motivo (ex.: SKU inválido) — importação parcial.</summary>
    public int SkippedItems { get; private set; }
    public string? ErrorMessage { get; private set; }

    public Guid RequestedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private XmlImport() { }

    private XmlImport(XmlImportId id, TenantId tenantId, Guid branchId, SupplierId? supplierId,
        string fileName, byte[] content, Guid requestedByUserId) : base(id)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Nome do arquivo é obrigatório.", nameof(fileName));

        TenantId = tenantId;
        BranchId = Common.BranchId.From(branchId);
        SupplierId = supplierId;
        FileName = fileName.Trim();
        Content = content ?? throw new ArgumentException("Conteúdo do XML é obrigatório.", nameof(content));
        Status = XmlImportStatus.Recebida;
        RequestedByUserId = requestedByUserId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static XmlImport Enqueue(TenantId tenantId, Guid branchId, Guid? supplierId,
        string fileName, byte[] content, Guid requestedByUserId)
    {
        if (content is null || content.Length == 0)
            throw new BusinessRuleViolationException("O arquivo XML enviado está vazio.");
        if (content.Length > 5 * 1024 * 1024)
            throw new BusinessRuleViolationException("O arquivo XML excede o limite de 5 MB.");

        return new XmlImport(
            XmlImportId.New(), tenantId, branchId,
            supplierId is null ? null : Common.SupplierId.From(supplierId.Value),
            fileName, content, requestedByUserId);
    }

    public void MarkProcessing()
    {
        EnsureStatus(XmlImportStatus.Recebida);
        Status = XmlImportStatus.Processando;
    }

    public void Complete(int totalItems, int processedItems, int skippedItems)
    {
        EnsureStatus(XmlImportStatus.Processando);
        TotalItems = totalItems;
        ProcessedItems = processedItems;
        SkippedItems = skippedItems;
        Status = skippedItems > 0 && processedItems == 0
            ? XmlImportStatus.Erro
            : XmlImportStatus.Concluida;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string error)
    {
        Status = XmlImportStatus.Erro;
        ErrorMessage = error?[..Math.Min(error.Length, 2000)];
        CompletedAtUtc = DateTime.UtcNow;
    }

    private void EnsureStatus(XmlImportStatus expected)
    {
        if (Status != expected)
            throw new BusinessRuleViolationException(
                $"Importação em estado inválido ({Status}); esperado {expected}.");
    }
}

