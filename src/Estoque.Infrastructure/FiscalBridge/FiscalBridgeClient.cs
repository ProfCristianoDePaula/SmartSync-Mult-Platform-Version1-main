using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Estoque.Application.FiscalBridge;
using Estoque.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Estoque.Infrastructure.FiscalBridge;

/// <summary>
/// Cliente HTTP do Fiscal com forward do JWT do usuário (mesmo padrão do
/// IdentityApiClient). Falhas viram BusinessRuleViolationException com a
/// mensagem do Fiscal (ou genérica, sem vazar detalhes).
/// </summary>
public sealed class FiscalBridgeClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<FiscalClientOptions> options,
    ILogger<FiscalBridgeClient> logger) : IFiscalBridgeClient
{
    public async Task<EmitenteFiscalView?> ObterEmitentePorFilialAsync(
        TenantId tenantId, Guid branchId, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get,
            $"api/fiscal/emitentes/por-filial/{branchId}", null, null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await ExigirSucessoAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<EmitenteFiscalView>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ProdutoFiscalView>> ObterPendenciasProdutosAsync(
        TenantId tenantId, IReadOnlyList<Guid> produtoIds, CancellationToken ct = default)
    {
        var query = string.Join("&", produtoIds.Distinct().Take(200).Select(id => $"produtoId={id}"));
        using var response = await SendAsync(HttpMethod.Get,
            $"api/fiscal/produtos/pendencias?{query}", null, null, ct);
        await ExigirSucessoAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<List<ProdutoFiscalView>>(cancellationToken: ct)
            ?? [];
    }

    public async Task<DocumentoFiscalView> CriarDocumentoAsync(
        TenantId tenantId, object body, string idempotencyKey, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post,
            "api/fiscal/documentos", body, idempotencyKey, ct);
        await ExigirSucessoAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<DocumentoFiscalView>(cancellationToken: ct))!;
    }

    public async Task<IReadOnlyList<DocumentoFiscalView>> ListarPorVendaAsync(
        TenantId tenantId, Guid vendaId, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get,
            $"api/fiscal/documentos/por-venda/{vendaId}", null, null, ct);
        await ExigirSucessoAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<List<DocumentoFiscalView>>(cancellationToken: ct)
            ?? [];
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("fiscal");
        client.BaseAddress = new Uri(options.Value.BaseUrl);

        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(auth))
            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(auth);

        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            return await client.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fiscal indisponível em {Path}.", path);
            throw new BusinessRuleViolationException("Módulo fiscal indisponível no momento.");
        }
    }

    private static async Task ExigirSucessoAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var detalhe = await response.Content.ReadAsStringAsync(ct);
        throw new BusinessRuleViolationException(
            $"Fiscal respondeu {(int)response.StatusCode}: {Truncar(detalhe)}");
    }

    private static string Truncar(string s) => s.Length <= 300 ? s : s[..300];
}
