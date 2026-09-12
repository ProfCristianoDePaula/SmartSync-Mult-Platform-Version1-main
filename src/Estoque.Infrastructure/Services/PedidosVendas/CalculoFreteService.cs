using Estoque.Application.Common;
using Estoque.Application.PedidosVendas;
using Estoque.Domain.Common;

namespace Estoque.Infrastructure.Services.PedidosVendas;

public interface ICalculoFreteServiceImpl : ICalculoFreteService { }

public sealed class CalculoFreteService : ICalculoFreteService
{
    public Task<decimal> CalcularAsync(string cep, TenantId tenantId, BranchId? unidadeId, IReadOnlyList<(Guid ProdutoId, decimal Quantidade)> itens, CancellationToken ct = default)
    {
        // Mock determinístico: CEP por região + peso por quantidade
        // Se CEP começa com 0-3 → frete 15, 4-6 → 20, 7-9 → 25; + 1 por item (quantidade total /2)
        var clean = cep.Replace("-", "").Trim();
        if (clean.Length != 8) return Task.FromResult(0m);
        var first = clean[0] - '0';
        decimal baseFrete = first <= 3 ? 15m : first <= 6 ? 20m : 25m;
        var qtdTotal = itens.Sum(i => i.Quantidade);
        var extra = Math.Round(qtdTotal * 0.5m, 2);
        var total = baseFrete + extra;
        return Task.FromResult(Math.Round(total, 2));
    }
}

public sealed class TenantParcelamentoProvider : ITenantParcelamentoProvider
{
    public Task<int?> GetMaxParcelasAsync(TenantId tenantId, CancellationToken ct = default)
    {
        // Sem tabela Loja/TenantConfig no Identity — fallback para FormaPagto.
        // Documentado como ponto de evolução futura: criar TenantConfig.MaxParcelas ou usar Plan limits.
        return Task.FromResult<int?>(null);
    }
}
