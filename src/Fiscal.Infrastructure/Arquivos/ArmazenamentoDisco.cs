using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Fiscal.Application.Arquivos;
using Fiscal.Domain.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Fiscal.Infrastructure.Arquivos;

public sealed partial class ArmazenamentoDisco(
    IOptions<ArmazenamentoOptions> options,
    IHostEnvironment env) : IArmazenamentoFiscal
{
    public async Task<string> GuardarAsync(
        TenantId tenantId, string pasta, string nomeArquivo, byte[] conteudo, CancellationToken ct = default)
    {
        var caminho = CaminhoDe(tenantId, pasta, nomeArquivo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        await File.WriteAllBytesAsync(caminho, conteudo, ct);
        var sha = Convert.ToHexString(SHA256.HashData(conteudo));
        await File.WriteAllTextAsync(caminho + ".sha256", sha, Encoding.ASCII, ct);
        return sha;
    }

    public async Task<ArquivoFiscal?> ObterAsync(
        TenantId tenantId, string pasta, string nomeArquivo, CancellationToken ct = default)
    {
        var caminho = CaminhoDe(tenantId, pasta, nomeArquivo);
        if (!File.Exists(caminho)) return null;
        var conteudo = await File.ReadAllBytesAsync(caminho, ct);
        var sha = Convert.ToHexString(SHA256.HashData(conteudo));
        return new ArquivoFiscal(conteudo, sha, ContentTypeDe(nomeArquivo));
    }

    public Task<IReadOnlyList<string>> ListarAsync(TenantId tenantId, string pasta, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(CaminhoDe(tenantId, pasta, "x"))!;
        if (!Directory.Exists(dir)) return Task.FromResult<IReadOnlyList<string>>([]);
        var nomes = Directory.GetFiles(dir)
            .Select(Path.GetFileName)!
            .Where(n => n is not null && !n.EndsWith(".sha256", StringComparison.Ordinal))
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(nomes);
    }

    private string CaminhoDe(TenantId tenantId, string pasta, string nomeArquivo)
    {
        var raiz = options.Value.Raiz;
        if (!Path.IsPathRooted(raiz))
            raiz = Path.Combine(env.ContentRootPath, raiz);

        if (!NomeValido().IsMatch(nomeArquivo))
            throw new BusinessRuleViolationException("Nome de arquivo inválido.");
        var pastaOk = new string(pasta.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(pastaOk))
            throw new BusinessRuleViolationException("Pasta inválida.");

        var agora = DateTime.UtcNow;
        return Path.Combine(raiz, tenantId.Value.ToString(), agora.ToString("yyyy"),
            agora.ToString("MM"), pastaOk, nomeArquivo);
    }

    private static string ContentTypeDe(string nome) => Path.GetExtension(nome).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".json" => "application/json",
        _ => "application/xml"
    };

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.\-]{0,100}$")]
    private static partial Regex NomeValido();

}


