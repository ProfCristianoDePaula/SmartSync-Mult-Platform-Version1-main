using Estoque.Application.Common;
using Estoque.Application.PedidosVendas;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using FluentValidation;

namespace Estoque.Infrastructure.Services.PedidosVendas;

public sealed class CheckoutService(
    IPedidoRepository pedidos,
    IProdutosPedidoRepository itensPedido,
    IFormaPagtoRepository formasPagto,
    ICalculoFreteService freteService,
    ITenantParcelamentoProvider parcelamentoProvider,
    IVendaService vendas) : ICheckoutService
{
    public async Task<CalculoFreteDto> CalcularFreteAsync(CalcularFreteCommand command, CancellationToken ct = default)
    {
        BranchId? unidade = command.IdUnidade is null ? null : BranchId.From(command.IdUnidade.Value);
        var carrinho = await pedidos.GetCarrinhoAbertoAsync(command.TenantId, command.ClienteId, unidade, ct);
        if (carrinho is null) throw new BusinessRuleViolationException("Carrinho não encontrado.");

        var itens = await itensPedido.ListByPedidoAsync(carrinho.Id, ct);
        if (itens.Count == 0) throw new BusinessRuleViolationException("Carrinho vazio.");

        var itensQuant = itens.Select(i => (i.IdProduto.Value, i.Quantidade)).ToList();
        var valor = await freteService.CalcularAsync(command.Cep, command.TenantId, unidade, itensQuant, ct);
        return new CalculoFreteDto(command.Cep, valor, "Cálculo mock por região + quantidade (ponto de evolução: integração Correios/Melhor Envio)");
    }

    public async Task<IReadOnlyList<FormaPagtoDto>> ListarFormasPagtoAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var formas = await formasPagto.ListAsync(ct);
        var limiteTenant = await parcelamentoProvider.GetMaxParcelasAsync(tenantId, ct);
        return formas.Select(f =>
        {
            var limite = limiteTenant is not null ? Math.Min(f.QtdMaximaParcelas, limiteTenant.Value) : f.QtdMaximaParcelas;
            return new FormaPagtoDto(f.Id.Value, f.Descricao, limite);
        }).ToList();
    }

    public async Task<VendaDto> FinalizarAsync(FinalizarCheckoutCommand command, CancellationToken ct = default)
        => await vendas.FinalizarCompraAsync(command.TenantId, command.ClienteId, command.IdUnidade is null ? null : BranchId.From(command.IdUnidade.Value), command.Cep, command.IdFormaPagto, command.QuantidadeParcelas, ct);
}
