using System.Text;
using Fiscal.Application.Auditing;
using Fiscal.Application.Cadastros;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using FluentValidation;

namespace Fiscal.Infrastructure.Cadastros;

public sealed class ProdutoFiscalService(
    IProdutoFiscalRepository produtos,
    FiscalDbContext db,
    IAuditLogger audit,
    IValidadorFiscal validador,
    IValidator<UpsertProdutoFiscalCommand> validator) : IProdutoFiscalService
{
    public async Task<ProdutoFiscalDto> UpsertAsync(UpsertProdutoFiscalCommand command, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var pf = await produtos.GetAsync(command.TenantId, command.ProdutoId, ct);
        if (pf is null)
        {
            pf = ProdutoFiscal.Create(command.TenantId, command.ProdutoId, command.Tipo);
            await produtos.AddAsync(pf, ct);
        }
        else
        {
            pf.DefinirTipo(command.Tipo);
        }

        if (command.Tipo == TipoItemFiscal.Mercadoria)
        {
            pf.DefinirMercadoria(command.Ncm, command.Cest, command.Origem,
                command.UnCom, command.UnTrib, command.Fator, command.Gtin,
                command.CfopDentro, command.CfopFora, command.CstIcms, command.Csosn,
                command.AliqIcms, command.CstPis, command.CstCofins, command.AliqPis,
                command.AliqCofins, command.CstIpi, command.AliqIpi,
                command.CClassTrib, command.CstIbsCbs);
        }
        else
        {
            pf.DefinirServico(command.ItemLc116, command.Nbs, command.CodTrib,
                command.AliqIss, command.CfopDentro, command.CfopFora);
        }

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(command.TenantId, pf, crt: null, ct);
    }

    public async Task<ProdutoFiscalDto?> GetAsync(TenantId tenantId, Guid produtoId, CancellationToken ct = default)
    {
        var pf = await produtos.GetAsync(tenantId, produtoId, ct);
        return pf is null ? null : await ToDtoAsync(tenantId, pf, crt: null, ct);
    }

    public async Task<IReadOnlyList<ProdutoFiscalDto>> PendenciasAsync(
        TenantId tenantId, IReadOnlyList<Guid> produtoIds, CrtFiscal? crt, CancellationToken ct = default)
    {
        var lista = new List<ProdutoFiscalDto>();
        foreach (var id in produtoIds.Distinct().Take(200))
        {
            var pf = await produtos.GetAsync(tenantId, id, ct);
            if (pf is null)
            {
                lista.Add(new ProdutoFiscalDto(id, TipoItemFiscal.Mercadoria, null, null, null,
                    false, false, false, ["Sem perfil fiscal cadastrado."]));
                continue;
            }
            lista.Add(await ToDtoAsync(tenantId, pf, crt, ct));
        }
        return lista;
    }

    public async Task<ProdutoCsvReport> ImportarCsvAsync(TenantId tenantId, Stream csv, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var linhas = new List<(int Linha, string[] Cols)>();
        string? line;
        var n = 0;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            n++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (n == 1 && line.TrimStart().StartsWith("produtoId", StringComparison.OrdinalIgnoreCase)) continue;
            linhas.Add((n, line.Split(';')));
        }

        if (linhas.Count == 0)
            throw new BusinessRuleViolationException("Arquivo vazio.");

        int criados = 0, atualizados = 0;
        var erros = new List<ProdutoCsvError>();

        foreach (var (linha, cols) in linhas)
        {
            if (cols.Length < 5)
            {
                erros.Add(new ProdutoCsvError(linha, "Esperado: produtoId;tipo;ncm;... (ver modelo)."));
                continue;
            }

            try
            {
                var row = ParseRow(cols) with { TenantId = tenantId };
                var existente = await produtos.GetAsync(tenantId, row.ProdutoId, ct);
                await UpsertAsync(row, ct);
                if (existente is null) criados++; else atualizados++;
            }
            catch (Exception ex) when (ex is ArgumentException or BusinessRuleViolationException or ValidationException or FormatException)
            {
                erros.Add(new ProdutoCsvError(linha, ex.Message.Split('.')[0]));
            }
        }

        await audit.LogAsync(tenantId, null, "fiscal.produtos.importados", "ProdutoFiscal",
            $"+{criados} ~{atualizados} x{erros.Count}", ct);

        return new ProdutoCsvReport(linhas.Count, criados, atualizados, erros.Count, erros);
    }

    private UpsertProdutoFiscalCommand ParseRow(string[] c)
    {
        string? S(int i) => c.Length > i && !string.IsNullOrWhiteSpace(c[i]) ? c[i].Trim() : null;
        decimal? D(int i) => c.Length > i && decimal.TryParse(c[i].Trim(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

        if (!Guid.TryParse(c[0].Trim(), out var produtoId))
            throw new FormatException("produtoId inválido.");
        if (!Enum.TryParse<TipoItemFiscal>(c[1].Trim(), true, out var tipo))
        {
            if (!int.TryParse(c[1].Trim(), out var ti) || ti is not (1 or 2))
                throw new FormatException("tipo inválido (Mercadoria|Servico|1|2).");
            tipo = (TipoItemFiscal)ti;
        }

        return new UpsertProdutoFiscalCommand(
            produtoId, tipo, S(2), S(3), S(4), S(5), S(6), D(7), S(8), S(9), S(10),
            S(11), S(12), D(13), null, null, null, null, null, null, null, null,
            S(14), S(15), S(16), D(17))
        {
            TenantId = default!,
        };
    }

    private async Task<ProdutoFiscalDto> ToDtoAsync(TenantId tenantId, ProdutoFiscal pf, CrtFiscal? crt, CancellationToken ct)
    {
        var nfe = await validador.ValidarProdutoAsync(tenantId, pf.ProdutoId, crt, TipoDocumentoFiscal.NFe55, ct);
        var nfce = await validador.ValidarProdutoAsync(tenantId, pf.ProdutoId, crt, TipoDocumentoFiscal.NFCe65, ct);
        var nfse = await validador.ValidarProdutoAsync(tenantId, pf.ProdutoId, crt, TipoDocumentoFiscal.NFSe, ct);
        return new ProdutoFiscalDto(pf.ProdutoId, pf.Tipo, pf.Ncm, pf.CfopDentroUf, pf.CfopForaUf,
            nfe.Count == 0, nfce.Count == 0, nfse.Count == 0, nfe);
    }
}
