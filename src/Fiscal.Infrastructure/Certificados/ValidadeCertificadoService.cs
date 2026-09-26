using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fiscal.Infrastructure.Certificados;

/// <summary>
/// Verificação de validades (chamada pelo worker diário e diretamente nos testes).
/// </summary>
public sealed class ValidadeCertificadoService(
    FiscalDbContext db,
    IAlertaRepository alertas,
    ILogger<ValidadeCertificadoService> logger)
{
    public async Task<int> VerificarAsync(CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var limite = agora.AddDays(30);
        var gerados = 0;

        var certs = await db.CertificadosDigitais.ToListAsync(ct);
        foreach (var cert in certs)
        {
            if (cert.Status == CertificadoStatus.Revogado) continue;

            CertificadoStatus? novo = null;
            string? tipo = null;
            if (cert.NotAfter <= agora)
            {
                novo = CertificadoStatus.Expirado;
                tipo = "fiscal.certificado.expirado";
            }
            else if (cert.NotAfter <= limite && cert.Status == CertificadoStatus.Ativo)
            {
                novo = CertificadoStatus.Expirando;
                tipo = "fiscal.certificado.expirando";
            }

            if (novo is null || tipo is null) continue;

            cert.MarcarStatus(novo.Value);
            var entityId = cert.Id.Value.ToString();
            if (!await alertas.ExisteNaoLidoAsync(cert.TenantId, tipo, entityId, ct))
            {
                await alertas.AddAsync(AlertaFiscal.Create(cert.TenantId, tipo,
                    $"Certificado {cert.Thumbprint} ({cert.CnpjDoCertificado}): {tipo.Split('.')[2]}.",
                    entityId), ct);
                gerados++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Validade de certificados verificada ({Gerados} alertas).", gerados);
        return gerados;
    }
}
