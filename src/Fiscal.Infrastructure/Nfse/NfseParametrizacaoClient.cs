using System.Net.Http.Json;
using Fiscal.Application.Nfse;
using Microsoft.Extensions.Caching.Memory;

namespace Fiscal.Infrastructure.Nfse;

/// <summary>
/// Cliente de parametrização/convênio NFS-e (interface + HTTP).
/// Falha ou instabilidade => resultado Desconhecido + cache curto (5 min);
/// nunca derruba o fluxo. mTLS por emitente chega na Fiscal-12; aqui o
/// handler é o padrão da factory (chamada real só manual — ver PENDENCIAS).
/// Testes usam HttpMessageHandler falso (sem rede).
/// </summary>
public sealed class NfseParametrizacaoClient(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache) : INfseParametrizacaoClient
{
    public async Task<ConvenioResult> ObterConvenioAsync(string baseUrl, string codigoIbge, CancellationToken ct = default)
    {
        var key = $"nfse:convenio:{codigoIbge}";
        if (cache.TryGetValue(key, out ConvenioResult? cached) && cached is not null)
            return cached;

        ConvenioResult result;
        try
        {
            var client = httpClientFactory.CreateClient("nfse-parametros");
            var response = await client.GetAsync(
                $"{baseUrl.TrimEnd('/')}/convenio/{codigoIbge.Trim()}", ct);
            if (!response.IsSuccessStatusCode)
                return CacheAndReturn(key, new ConvenioResult(false, null));

            var payload = await response.Content.ReadFromJsonAsync<ConvenioPayload>(cancellationToken: ct);
            result = new ConvenioResult(true, payload?.Situacao);
        }
        catch
        {
            result = new ConvenioResult(false, null);
        }

        return CacheAndReturn(key, result);
    }

    public async Task<ParametrosResult> ObterParametrosAsync(
        string baseUrl, string codigoIbge, string? codigoServico, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("nfse-parametros");
            var url = $"{baseUrl.TrimEnd('/')}/parametros_municipais/{codigoIbge.Trim()}/{codigoServico?.Trim()}";
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return new ParametrosResult(false, null);

            var body = await response.Content.ReadAsStringAsync(ct);
            return new ParametrosResult(true, body);
        }
        catch
        {
            return new ParametrosResult(false, null);
        }
    }

    private ConvenioResult CacheAndReturn(string key, ConvenioResult result)
    {
        cache.Set(key, result, TimeSpan.FromMinutes(5));
        return result;
    }

    private sealed record ConvenioPayload(string? Situacao);
}
