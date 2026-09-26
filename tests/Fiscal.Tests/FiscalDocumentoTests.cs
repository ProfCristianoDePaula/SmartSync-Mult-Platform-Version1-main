using Fiscal.Application.Documentos;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Domain.ValueObjects;
using Fiscal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Núcleo do documento (Fiscal-7): 50 reservas concorrentes únicas e
/// consecutivas, transições inválidas, DV da chave, snapshot imutável e
/// não-reutilização de número rejeitado — Postgres real.
/// </summary>
[Collection("integration")]
public sealed class FiscalDocumentoTests(FiscalApiFixture fixture)
{
    [Fact]
    public async Task ReservasConcorrentes_GeramNumerosUnicosEConsecutivos()
    {
        const int total = 50;
        var emitente = EmitenteFiscalId.New();

        var resultados = new int[total];
        await Parallel.ForEachAsync(Enumerable.Range(0, total), async (i, ct) =>
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ISerieNumeracaoService>();
            resultados[i] = await svc.ReservarAsync(emitente, 55, "1", AmbienteFiscal.Homologacao, ct);
        });

        Assert.Equal(total, resultados.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, total), resultados.OrderBy(x => x));
    }

    [Fact]
    public async Task Transicoes_Validas_E_Invalidas()
    {
        var doc = DocumentoFiscal.Criar(
            TenantId.New(), EmitenteFiscalId.New(), TipoDocumentoFiscal.NFe55,
            AmbienteFiscal.Homologacao, "1", 1, null, null, null,
            "idem-1", "hash-1", "{}", "{}", "[]", "{}");

        doc.TransicionarPara(StatusDocumentoFiscal.Validando);
        doc.TransicionarPara(StatusDocumentoFiscal.Assinado);
        Assert.Equal(StatusDocumentoFiscal.Assinado, doc.Status);

        Assert.Throws<BusinessRuleViolationException>(() =>
            doc.TransicionarPara(StatusDocumentoFiscal.Autorizado)); // Assinado→Autorizado proibido
        Assert.Throws<BusinessRuleViolationException>(() =>
            doc.TransicionarPara(StatusDocumentoFiscal.Pendente)); // sem retorno
    }

    [Fact]
    public void ChaveAcesso_MontaValida_E_TamperQuebra()
    {
        var chave = ChaveAcesso.Montar(35, 2026, 9, "11222333000181", 55, 1, 123, 1, 12345678);
        Assert.Equal(44, chave.Numero.Length);
        Assert.Equal(35, chave.Cuf);
        Assert.Equal(55, chave.Modelo);
        Assert.Equal(1, chave.Serie);
        Assert.Equal(123, chave.NumeroDocumento);

        var validada = ChaveAcesso.Validar(chave.Numero);
        Assert.Equal(chave.Numero, validada.Numero);

        var adulterada = chave.Numero[..10] + (chave.Numero[10] == '0' ? '1' : '0') + chave.Numero[11..];
        Assert.Throws<ArgumentException>(() => ChaveAcesso.Validar(adulterada));
        Assert.Throws<BusinessRuleViolationException>(() =>
            ChaveAcesso.Montar(35, 2026, 9, "ABCD1234000199", 55, 1, 1, 1, 1)); // alfa → F0-01
    }

    [Fact]
    public void Snapshot_Imutavel_Renumeracao_NaoReutiliza()
    {
        var doc = DocumentoFiscal.Criar(
            TenantId.New(), EmitenteFiscalId.New(), TipoDocumentoFiscal.NFe55,
            AmbienteFiscal.Homologacao, "1", 41, "chave41", null, null,
            "idem-2", "hash-2", "{emit}", "{dest}", "[itens]", "{totais}");

        doc.TransicionarPara(StatusDocumentoFiscal.Validando);
        doc.TransicionarPara(StatusDocumentoFiscal.Rejeitado);
        doc.Renumerar(42, "chave42");

        Assert.Equal(42, doc.Numero);
        Assert.Equal("{emit}", doc.SnapshotEmitente);
        Assert.Equal("[itens]", doc.SnapshotItens);
        Assert.Throws<ArgumentException>(() => doc.Renumerar(42, "x")); // sem retrocesso
    }

    [Fact]
    public async Task Idempotencia_ChaveUnica_NoBanco()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FiscalDbContext>();

        var tenant = TenantId.New();
        db.DocumentosFiscais.Add(DocumentoFiscal.Criar(
            tenant, EmitenteFiscalId.New(), TipoDocumentoFiscal.NFe55,
            AmbienteFiscal.Homologacao, "1", 1, null, null, null,
            "idem-dup", "hash-a", "{}", "{}", "[]", "{}"));
        await db.SaveChangesAsync();

        db.DocumentosFiscais.Add(DocumentoFiscal.Criar(
            tenant, EmitenteFiscalId.New(), TipoDocumentoFiscal.NFe55,
            AmbienteFiscal.Homologacao, "1", 2, null, null, null,
            "idem-dup", "hash-b", "{}", "{}", "[]", "{}"));
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
    }
}
