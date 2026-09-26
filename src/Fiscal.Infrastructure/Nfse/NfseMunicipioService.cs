using System.Security.Cryptography;
using System.Text;
using Fiscal.Application.Auditing;
using Fiscal.Application.Nfse;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using FluentValidation;

namespace Fiscal.Infrastructure.Nfse;

public sealed class NfseMunicipioService(
    IMunicipioRepository municipios,
    INfseMunicipioConfigRepository configs,
    INfseAmbienteRepository ambientes,
    FiscalDbContext db,
    IAuditLogger audit,
    IValidator<NfseManualOverrideCommand> overrideValidator,
    IValidator<NfseAmbienteUpsertCommand> ambienteValidator) : INfseMunicipioService
{
    private const int MinimoLinhas = 5;

    public async Task<NfseImportReport> ImportarCsvAsync(
        Stream csv, string nomeArquivo, Guid userId, bool ignorarMinimo = false, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var linhas = new List<(int Linha, string[] Cols)>();
        string? line;
        var n = 0;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            n++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (n == 1 && line.TrimStart().StartsWith("codigo_ibge", StringComparison.OrdinalIgnoreCase)) continue; // cabeçalho
            linhas.Add((n, line.Split(';')));
        }

        if (linhas.Count == 0)
            throw new BusinessRuleViolationException("Arquivo vazio: nenhuma linha de dados.");
        if (!ignorarMinimo && linhas.Count < MinimoLinhas)
            throw new BusinessRuleViolationException(
                $"Arquivo suspeitamente pequeno ({linhas.Count} linhas, mínimo {MinimoLinhas}). " +
                "Nada foi alterado. Use ignorarMinimo=true para forçar.");

        int criados = 0, atualizados = 0, ignorados = 0;
        var erros = new List<NfseImportError>();

        foreach (var (linha, cols) in linhas)
        {
            if (cols.Length < 4)
            {
                erros.Add(new NfseImportError(linha, "Esperado: codigo_ibge;nome;uf;situacao[;fonte]."));
                continue;
            }

            var ibge = Norm(cols[0]);
            var nome = cols[1].Trim();
            var uf = cols[2].Trim().ToUpperInvariant();
            var situacao = cols[3].Trim();
            var fonte = cols.Length > 4 ? cols[4].Trim() : null;

            if (ibge.Length != 7 || string.IsNullOrWhiteSpace(nome) || uf.Length != 2)
            {
                erros.Add(new NfseImportError(linha, "IBGE (7 dígitos), nome e UF (2 letras) obrigatórios."));
                continue;
            }

            if (!TryParseModo(situacao, out var modo))
            {
                erros.Add(new NfseImportError(linha, $"Situação desconhecida: '{situacao}'. Use nacional|adn|municipal|nao|desconhecido."));
                continue;
            }

            try
            {
                var mun = await municipios.GetByIbgeAsync(ibge, ct);
                if (mun is null)
                {
                    await municipios.AddAsync(Municipio.Create(ibge, nome, uf), ct);
                    criados++;
                }
                else if (!mun.Nome.Equals(nome, StringComparison.Ordinal) || mun.Uf != uf)
                {
                    mun.Update(nome, uf);
                    atualizados++;
                }

                var cfg = await configs.GetByIbgeAsync(ibge, ct);
                if (cfg is null)
                {
                    await configs.AddAsync(NfseMunicipioConfig.Create(ibge, modo, fonte), ct);
                    criados++;
                }
                else if (cfg.SobrescritoManual)
                {
                    ignorados++;
                }
                else if (cfg.Modo != modo)
                {
                    cfg.AplicarImportacao(modo, fonte, cfg.Observacao);
                    atualizados++;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or BusinessRuleViolationException)
            {
                erros.Add(new NfseImportError(linha, ex.Message));
            }
        }

        var total = linhas.Count;
        var rejeitados = erros.Count;

        using var sha = SHA256.Create();
        csv.Position = 0;
        var hash = Convert.ToHexString(await sha.ComputeHashAsync(csv, ct));

        db.NfseImportRuns.Add(NfseImportRun.Create(userId, hash, total, criados, atualizados, ignorados, rejeitados));
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.nfse.importados", "Municipio",
            $"{nomeArquivo}: +{criados} ~{atualizados} manu:{ignorados} x{rejeitados}", ct);

        return new NfseImportReport(total, criados, atualizados, ignorados, rejeitados, hash, erros);
    }

    public async Task<NfseSituacaoDto?> ObterSituacaoAsync(string codigoIbge, CancellationToken ct = default)
    {
        var ibge = Norm(codigoIbge);
        var mun = await municipios.GetByIbgeAsync(ibge, ct);
        if (mun is null) return null;

        var cfg = await configs.GetByIbgeAsync(ibge, ct)
            ?? NfseMunicipioConfig.Create(ibge, ModoEmissaoNfse.Desconhecido, null);

        var ambs = await ambientes.ListByIbgeAsync(ibge, ct);
        var relevantes = ambs
            .Where(a => a.CodigoIbge == "*" ? a.Modo == cfg.Modo : true)
            .Select(ToAmbDto).ToList();

        return new NfseSituacaoDto(
            new MunicipioDto(mun.Id.Value, mun.CodigoIbge, mun.Nome, mun.Uf, mun.IsActive),
            new NfseMunicipioConfigDto(cfg.CodigoIbge, cfg.Modo, cfg.Fonte, cfg.AtualizadoEm, cfg.SobrescritoManual, cfg.Observacao),
            relevantes,
            MensagemPara(cfg.Modo, mun));
    }

    public async Task<NfseMunicipioConfigDto> OverrideManualAsync(NfseManualOverrideCommand command, Guid userId, CancellationToken ct = default)
    {
        await overrideValidator.ValidateAndThrowAsync(command, ct);
        var ibge = Norm(command.CodigoIbge);

        if (await municipios.GetByIbgeAsync(ibge, ct) is null)
            throw new BusinessRuleViolationException("Município não cadastrado.");

        var cfg = await configs.GetByIbgeAsync(ibge, ct);
        if (cfg is null)
        {
            cfg = NfseMunicipioConfig.Create(ibge, command.Modo, null, sobrescritoManual: false, command.Observacao);
            await configs.AddAsync(cfg, ct);
        }

        cfg.AplicarOverrideManual(command.Modo, command.Observacao, command.Motivo);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.nfse.override", "NfseMunicipioConfig", ibge, ct);

        return new NfseMunicipioConfigDto(cfg.CodigoIbge, cfg.Modo, cfg.Fonte, cfg.AtualizadoEm, cfg.SobrescritoManual, cfg.Observacao);
    }

    public async Task<NfseAmbienteDto> UpsertAmbienteAsync(NfseAmbienteUpsertCommand command, Guid userId, CancellationToken ct = default)
    {
        await ambienteValidator.ValidateAndThrowAsync(command, ct);
        var ibge = command.CodigoIbge.Trim();

        var amb = await ambientes.FindAsync(ibge, command.Modo, command.Ambiente, ct);
        if (amb is null)
        {
            amb = NfseAmbiente.Create(ibge, command.Modo, command.Ambiente,
                command.BaseUrlSefin, command.BaseUrlAdn, command.BaseUrlParametros, command.Verificado);
            await ambientes.AddAsync(amb, ct);
        }
        else
        {
            amb.Update(command.BaseUrlSefin, command.BaseUrlAdn, command.BaseUrlParametros, command.Verificado);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.nfse.ambiente", "NfseAmbiente", $"{ibge}/{(int)command.Ambiente}", ct);

        return ToAmbDto(amb);
    }

    public async Task<PagedNfseMunicipios> ListarAsync(ListMunicipiosQuery query, CancellationToken ct = default)
    {
        var (items, total) = await municipios.ListAsync(query.Uf, query.Search, query.Page, query.PageSize, ct);
        var cfgs = await configs.ListByIbgesAsync(items.Select(m => m.CodigoIbge), ct);
        var porIbge = cfgs.ToDictionary(c => c.CodigoIbge);

        var list = items.Select(m =>
        {
            porIbge.TryGetValue(m.CodigoIbge, out var c);
            return new NfseMunicipioListItem(m.CodigoIbge, m.Nome, m.Uf,
                c?.Modo ?? ModoEmissaoNfse.Desconhecido, c?.SobrescritoManual ?? false);
        }).ToList();

        if (query.Modo is not null)
            list = list.Where(i => i.Modo == query.Modo).ToList();

        return new PagedNfseMunicipios(list, query.Page, query.PageSize, total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.PageSize));
    }

    internal static bool TryParseModo(string situacao, out ModoEmissaoNfse modo)
    {
        var s = situacao.Trim().ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
        modo = s switch
        {
            "nacional" or "nacionalemissorpublico" or "emissorpublico" or "completo" => ModoEmissaoNfse.NacionalEmissorPublico,
            "adn" or "somenteadn" or "integradoadn" => ModoEmissaoNfse.SomenteAdn,
            "municipal" or "provedormunicipal" or "proprio" => ModoEmissaoNfse.ProvedorMunicipal,
            "nao" or "naoaderiu" or "naoaderido" => ModoEmissaoNfse.NaoAderiu,
            "desconhecido" or "desconhecida" or "" => ModoEmissaoNfse.Desconhecido,
            _ => (ModoEmissaoNfse)(-1)
        };
        return (int)modo >= 0;
    }

    private static string MensagemPara(ModoEmissaoNfse modo, Municipio mun) => modo switch
    {
        ModoEmissaoNfse.NacionalEmissorPublico =>
            $"Emissão pelo Padrão Nacional disponível para {mun.Nome}/{mun.Uf} (verifique o ambiente do emitente).",
        ModoEmissaoNfse.SomenteAdn =>
            $"Seu município ({mun.Nome}/{mun.Uf}) mantém emissor próprio; a emissão pelo SmartSync não está disponível ainda.",
        ModoEmissaoNfse.ProvedorMunicipal =>
            $"Seu município ({mun.Nome}/{mun.Uf}) usa provedor municipal; conector específico ainda não implementado.",
        ModoEmissaoNfse.NaoAderiu =>
            $"Município ({mun.Nome}/{mun.Uf}) não aderiu ao convênio nacional.",
        _ => $"Situação de {mun.Nome}/{mun.Uf} desconhecida: consulte a lista oficial ou aguarde sincronização."
    };

    private static NfseAmbienteDto ToAmbDto(NfseAmbiente a) => new(
        a.Id.Value, a.CodigoIbge, a.Modo, a.Ambiente,
        a.BaseUrlSefin, a.BaseUrlAdn, a.BaseUrlParametros, a.Verificado);

    private static string Norm(string ibge) => new((ibge ?? "").Where(char.IsDigit).ToArray());
}
