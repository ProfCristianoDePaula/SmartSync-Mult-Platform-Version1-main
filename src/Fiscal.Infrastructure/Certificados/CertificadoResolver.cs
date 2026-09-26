using System.Security.Cryptography.X509Certificates;
using System.Text;
using Fiscal.Application.Certificados;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;

namespace Fiscal.Infrastructure.Certificados;

/// <summary>
/// Resolve filial › tenant e devolve X509 descartável por chamada.
/// Rotação oportunista: se o envelope não usa a chave ativa, re-envelopa.
/// </summary>
public sealed class CertificadoResolver(
    ICertificadoRepository certificados,
    ISecretProtector cofre,
    Fiscal.Infrastructure.Persistence.FiscalDbContext db) : ICertificadoResolver
{
    public async Task<X509Certificate2> ObterAsync(
        TenantId tenantId, BranchId? branchId, AmbienteFiscal ambiente,
        CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;

        CertificadoDigital? cert = null;
        if (branchId is not null)
            cert = (await certificados.ListAtivosAsync(tenantId, branchId.Value.Value, ct))
                .Where(c => c.NotAfter > agora && c.NotBefore <= agora)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefault();

        cert ??= (await certificados.ListAtivosAsync(tenantId, Guid.Empty, ct))
            .Where(c => c.NotAfter > agora && c.NotBefore <= agora)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefault();

        if (cert is null)
            throw new BusinessRuleViolationException("Nenhum certificado válido para o emitente.");

        var pfx = cofre.Unprotect(cert.KeyId, cert.PfxCifrado);
        var senha = Encoding.UTF8.GetString(cofre.Unprotect(cert.KeyId, cert.SenhaCifrada));

        if (cert.KeyId != cofre.KeyIdAtual)
            cert.Reenvelopar(cofre.Protect(pfx), cofre.Protect(Encoding.UTF8.GetBytes(senha)), cofre.KeyIdAtual);

        await db.SaveChangesAsync(ct);

        return X509CertificateLoader.LoadPkcs12(pfx, senha, X509KeyStorageFlags.EphemeralKeySet);
    }
}
