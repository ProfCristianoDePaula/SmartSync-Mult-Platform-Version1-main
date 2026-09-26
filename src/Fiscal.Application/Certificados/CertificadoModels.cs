using FluentValidation;

namespace Fiscal.Application.Certificados;

// SÓ metadados — PFX/senha jamais aparecem aqui (R4).
public sealed record CertificadoDto(
    Guid Id,
    Guid? BranchId,
    string CnpjDoCertificado,
    string Thumbprint,
    string Subject,
    DateTime NotBefore,
    DateTime NotAfter,
    string KeyId,
    int Status,
    DateTime CreatedAtUtc);

public sealed record AlertaDto(
    Guid Id,
    string Tipo,
    string Mensagem,
    string? EntityId,
    bool Lida,
    DateTime CreatedAtUtc);

public sealed record UploadCertificadoCommand(
    byte[] Arquivo,
    string NomeArquivo,
    string Senha,
    Guid? BranchId)
{
    public UploadCertificadoCommand WithTenant(Fiscal.Domain.Common.TenantId tenantId) => this with { TenantId = tenantId };
    public Fiscal.Domain.Common.TenantId TenantId { get; init; }
}

public sealed class UploadCertificadoCommandValidator : AbstractValidator<UploadCertificadoCommand>
{
    public UploadCertificadoCommandValidator()
    {
        RuleFor(x => x.Arquivo).NotNull()
            .Must(a => a is { Length: > 0 and <= 1 * 1024 * 1024 })
            .WithMessage("Arquivo PFX/P12 obrigatório (≤1 MB).");
        RuleFor(x => x.NomeArquivo).Must(n =>
                n.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) ||
                n.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Apenas arquivos .pfx/.p12 (A1). A3/nuvem indisponíveis nesta versão.");
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Senha do certificado é obrigatória.");
    }
}

public interface ICertificadoService
{
    Task<CertificadoDto> UploadAsync(UploadCertificadoCommand command, Guid userId, string role, CancellationToken ct = default);
    Task<IReadOnlyList<CertificadoDto>> ListAsync(Fiscal.Domain.Common.TenantId tenantId, CancellationToken ct = default);
    Task<CertificadoDto?> GetByIdAsync(Fiscal.Domain.Common.TenantId tenantId, Guid id, CancellationToken ct = default);
    Task<bool> RevogarAsync(Fiscal.Domain.Common.TenantId tenantId, Guid id, Guid userId, CancellationToken ct = default);
}

public interface IAlertaService
{
    Task<IReadOnlyList<AlertaDto>> ListAsync(Fiscal.Domain.Common.TenantId? tenantId, bool apenasNaoLidas, CancellationToken ct = default);
    Task<bool> MarcarLidaAsync(Fiscal.Domain.Common.TenantId? tenantId, Guid id, CancellationToken ct = default);
}
