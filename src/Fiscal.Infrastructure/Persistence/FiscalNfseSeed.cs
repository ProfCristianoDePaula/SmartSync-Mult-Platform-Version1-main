using System.Reflection;
using System.Text.Json;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Persistence;

/// <summary>
/// Seeds NFS-e (Fiscal-3): municípios verificados do JSON embarcado +
/// ambientes padrão do modo nacional (hosts do roteiro §3 — verificado=false
/// até reconfirmação no portal, ver PENDENCIAS.md F0-15).
/// </summary>
public static class FiscalNfseSeed
{
    public static async Task SeedAsync(FiscalDbContext db, CancellationToken ct = default)
    {
        if (!await db.Municipios.AnyAsync(ct))
        {
            foreach (var m in CarregarMunicipios())
            {
                if (await db.Municipios.AnyAsync(x => x.CodigoIbge == m.CodigoIbge, ct)) continue;
                await db.Municipios.AddAsync(Municipio.Create(m.CodigoIbge, m.Nome, m.Uf), ct);
                await db.NfseMunicipioConfigs.AddAsync(
                    NfseMunicipioConfig.Create(m.CodigoIbge, ModoEmissaoNfse.Desconhecido,
                        "seed inicial Fiscal-3 (verificar adesão na lista oficial)"), ct);
            }
            await db.SaveChangesAsync(ct);
        }

        if (!await db.NfseAmbientes.AnyAsync(ct))
        {
            const string fonte = "https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/apis-prod-restrita-e-producao";
            _ = fonte;
            await db.NfseAmbientes.AddRangeAsync(new[]
            {
                NfseAmbiente.Create("*", ModoEmissaoNfse.NacionalEmissorPublico, AmbienteFiscal.Homologacao,
                    "https://sefin.producaorestrita.nfse.gov.br",
                    "https://adn.producaorestrita.nfse.gov.br", null, verificado: false),
                NfseAmbiente.Create("*", ModoEmissaoNfse.NacionalEmissorPublico, AmbienteFiscal.Producao,
                    "https://sefin.nfse.gov.br",
                    "https://adn.nfse.gov.br", null, verificado: false),
            }, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    private static List<MunicipioSeed> CarregarMunicipios()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("Fiscal.Infrastructure.Seeds.municipios-ibge.json")
            ?? throw new InvalidOperationException("Seed municipios-ibge.json não embutido.");
        return JsonSerializer.Deserialize<List<MunicipioSeed>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Seed municipios-ibge.json inválido.");
    }

    private sealed record MunicipioSeed(string CodigoIbge, string Nome, string Uf);
}
