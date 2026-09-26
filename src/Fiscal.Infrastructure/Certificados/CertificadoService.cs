using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Fiscal.Application.Auditing;
using Fiscal.Application.Certificados;
using Fiscal.Application.Emitentes;
using Fiscal.Application.IntegrationServices;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using FluentValidation;

namespace Fiscal.Infrastructure.Certificados;

public sealed partial class CertificadoService(
    ICertificadoRepository certificados,
    IEmitenteFiscalRepository emitentes,
    IFilialAccessChecker filiais,
    FiscalDbContext db,
    IAuditLogger audit,
    ISecretProtector cofre,
    IValidator<UploadCertificadoCommand> validator) : ICertificadoService
{
    public async Task<CertificadoDto> UploadAsync(
        UploadCertificadoCommand command, Guid userId, string role, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        ExigirPapelConfigurador(role);

        var branchId = command.BranchId ?? Guid.Empty;

        // Filial do escopo precisa pertencer ao tenant (fail-closed).
        if (command.BranchId is not null
            && await filiais.ValidateAsync(command.TenantId, command.BranchId.Value, ct) != FilialAccess.Allowed)
            throw new BusinessRuleViolationException("Filial não pertence a este tenant.");

        // Carrega SEM persistir chave no disco; EphemeralKeySet não dispensa proteger o PFX.
        X509Certificate2 cert;
        try
        {
            cert = X509CertificateLoader.LoadPkcs12(command.Arquivo, command.Senha,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch
        {
            // Senha errada ou PFX inválido — sem vazar detalhes (R4).
            throw new BusinessRuleViolationException("Não foi possível abrir o certificado (senha ou arquivo inválido).");
        }

        using (cert)
        {
            if (!cert.HasPrivateKey)
                throw new BusinessRuleViolationException("Certificado sem chave privada.");

            var agora = DateTime.UtcNow;
            if (cert.NotBefore.ToUniversalTime() > agora || cert.NotAfter.ToUniversalTime() <= agora)
                throw new BusinessRuleViolationException("Certificado fora da validade.");

            var cnpj = ExtrairCnpj(cert)
                ?? throw new BusinessRuleViolationException(
                    "CNPJ do certificado não identificado (esperado ICP-Brasil no subject/SAN).");

            await ExigirBaseCompativelAsync(command.TenantId, branchId, cnpj, ct);

            var pfxCifrado = cofre.Protect(command.Arquivo);
            var senhaCifrada = cofre.Protect(System.Text.Encoding.UTF8.GetBytes(command.Senha));

            // Rotação: novo upload no mesmo escopo revoga o anterior (histórico mantido).
            foreach (var anterior in await certificados.ListAtivosAsync(command.TenantId, branchId, ct))
                anterior.Revogar();

            var novo = CertificadoDigital.Create(command.TenantId, branchId, cnpj,
                cert.Thumbprint, cert.Subject,
                cert.NotBefore.ToUniversalTime(), cert.NotAfter.ToUniversalTime(),
                pfxCifrado, senhaCifrada, cofre.KeyIdAtual, userId);

            await certificados.AddAsync(novo, ct);
            await db.SaveChangesAsync(ct);

            // Array.Clear não é chamado na senha em memória por limitação do fluxo;
            // o PFX/senha nunca vão para log, resposta ou exceção (R4).
            await audit.LogAsync(command.TenantId, userId, "fiscal.certificado.enviado",
                "CertificadoDigital", novo.Id.Value.ToString(), ct);

            return ToDto(novo);
        }
    }

    public async Task<IReadOnlyList<CertificadoDto>> ListAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var items = await certificados.ListByTenantAsync(tenantId, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<CertificadoDto?> GetByIdAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
    {
        var cert = await certificados.GetAsync(tenantId, id, ct);
        return cert is null ? null : ToDto(cert);
    }

    public async Task<bool> RevogarAsync(TenantId tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var cert = await certificados.GetAsync(tenantId, id, ct);
        if (cert is null) return false;

        cert.Revogar();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, userId, "fiscal.certificado.revogado",
            "CertificadoDigital", id.ToString(), ct);
        return true;
    }

    private async Task ExigirBaseCompativelAsync(TenantId tenantId, Guid branchId, string cnpj, CancellationToken ct)
    {
        var baseCert = cnpj[..8];

        if (branchId != Guid.Empty)
        {
            var emitente = await emitentes.GetByBranchAsync(tenantId, BranchId.From(branchId), ct);
            if (emitente is null)
                throw new BusinessRuleViolationException("Filial sem emitente fiscal.");
            if (!string.Equals(emitente.Cnpj.Numero[..8], baseCert, StringComparison.Ordinal))
                throw new BusinessRuleViolationException(
                    "CNPJ-base do certificado diverge do emitente da filial.", "fiscal.certificado.base-divergente");
            return;
        }

        // Escopo tenant: ao menos um emitente com a mesma base (matriz/filiais do mesmo CNPJ-base).
        var todos = await emitentes.ListAsync(tenantId, ct);
        if (todos.Count == 0)
            throw new BusinessRuleViolationException("Tenant sem emitente fiscal.");
        if (todos.All(e => !string.Equals(e.Cnpj.Numero[..8], baseCert, StringComparison.Ordinal)))
            throw new BusinessRuleViolationException(
                "CNPJ-base do certificado diverge dos emitentes do tenant.", "fiscal.certificado.base-divergente");
    }

    private static void ExigirPapelConfigurador(string role)
    {
        if (string.Equals(role, PlatformRoles.TenantAdmin, StringComparison.Ordinal)
            || string.Equals(role, PlatformRoles.Manager, StringComparison.Ordinal))
            return;
        throw new UnauthorizedAccessException("Somente TenantAdmin/Manager gerenciam certificados.");
    }

    /// <summary>CNPJ ICP-Brasil: 14 dígitos no subject ou SAN. Sem ele, 422.</summary>
    internal static string? ExtrairCnpj(X509Certificate2 cert)
    {
        var m = CnpjRegex().Match(cert.Subject ?? "");
        if (m.Success) return m.Value;
        foreach (var ext in cert.Extensions.OfType<X509SubjectAlternativeNameExtension>())
        {
            var mm = CnpjRegex().Match(ext.Format(false));
            if (mm.Success) return mm.Value;
        }
        return null;
    }

    [GeneratedRegex(@"\d{14}")]
    private static partial Regex CnpjRegex();

    internal static CertificadoDto ToDto(CertificadoDigital c) => new(
        c.Id.Value, c.BranchId == Guid.Empty ? null : c.BranchId,
        c.CnpjDoCertificado, c.Thumbprint, c.Subject,
        c.NotBefore, c.NotAfter, c.KeyId, (int)c.Status, c.CreatedAtUtc);
}
