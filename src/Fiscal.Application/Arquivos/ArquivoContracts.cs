using Fiscal.Domain.Common;

namespace Fiscal.Application.Arquivos;

/// <summary>
/// Armazenamento fiscal (Fiscal-11): XML enviado/autorizado, eventos e
/// respostas, com hash SHA-256 e controle de acesso por tenant. Implementação
/// em disco/volume por tenant/ano/mês/chave; interface pronta para
/// S3-compatível. NUNCA apagar (R9); retenção mínima configurável.
/// </summary>
public interface IArmazenamentoFiscal
{
    /// <returns>SHA-256 hex do conteúdo gravado.</returns>
    Task<string> GuardarAsync(TenantId tenantId, string pasta, string nomeArquivo, byte[] conteudo, CancellationToken ct = default);
    Task<ArquivoFiscal?> ObterAsync(TenantId tenantId, string pasta, string nomeArquivo, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListarAsync(TenantId tenantId, string pasta, CancellationToken ct = default);
}

public sealed record ArquivoFiscal(byte[] Conteudo, string Sha256, string ContentType);

/// <summary>DANFE/DANFCE simplificado a partir do documento oficial.</summary>
public interface IDanfeGerador
{
    /// <summary>Gera o PDF simplificado (com marcação de homologação quando for o caso).</summary>
    byte[] Gerar(Fiscal.Domain.Entities.DocumentoFiscal documento);
}
