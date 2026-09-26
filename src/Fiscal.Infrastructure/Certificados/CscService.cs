using System.Text;
using Fiscal.Application.Auditing;
using Fiscal.Application.Certificados;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Certificados;

public sealed class CscService(
    IEmitenteFiscalRepository emitentes,
    IConfiguracaoDocumentoRepository configs,
    FiscalDbContext db,
    IAuditLogger audit,
    ISecretProtector cofre) : ICscService
{
    public async Task DefinirTokenAsync(
        TenantId tenantId, Guid emitenteId, TipoDocumentoFiscal tipo,
        string cscId, string token, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cscId))
            throw new BusinessRuleViolationException("CSC id obrigatório.");
        if (string.IsNullOrWhiteSpace(token))
            throw new BusinessRuleViolationException("Token CSC obrigatório.");

        var emitente = await emitentes.GetAsync(tenantId, emitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");

        var cfg = await configs.FindAsync(emitente.Id, tipo, AmbienteFiscal.Homologacao, ct)
            ?? throw new BusinessRuleViolationException("Configuração do tipo/ambiente inexistente.");

        cfg.Update(cfg.Habilitado, cfg.Serie, cfg.ModoIntegracao, cfg.ReferenciaCertificado, cscId.Trim());
        cfg.DefinirCscToken(
            cofre.Protect(Encoding.UTF8.GetBytes(token)),
            cofre.KeyIdAtual);

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, userId, "fiscal.csc.definido", "ConfiguracaoDocumento",
            $"{emitenteId}/{(int)tipo}", ct);
    }

    public async Task<(string CscId, string Token)?> ObterAsync(
        TenantId tenantId, Guid emitenteId, TipoDocumentoFiscal tipo,
        CancellationToken ct = default)
    {
        var emitente = await emitentes.GetAsync(tenantId, emitenteId, ct);
        if (emitente is null) return null;

        // Token efetivo: homologação ou produção, o que estiver habilitado.
        foreach (var amb in new[] { AmbienteFiscal.Homologacao, AmbienteFiscal.Producao })
        {
            var cfg = await configs.FindAsync(emitente.Id, tipo, amb, ct);
            if (cfg?.CscTokenCifrado is null || cfg.CscTokenKeyId is null || cfg.CscId is null)
                continue;
            var token = Encoding.UTF8.GetString(cofre.Unprotect(cfg.CscTokenKeyId, cfg.CscTokenCifrado));
            return (cfg.CscId, token);
        }

        return null;
    }
}
