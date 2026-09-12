using Estoque.Application.Common;
using Estoque.Application.PedidosVendas;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;

namespace Estoque.Infrastructure.Services.PedidosVendas;

public sealed class VendaService(
    IPedidoRepository pedidos,
    IProdutosPedidoRepository itensPedido,
    IVendaRepository vendas,
    IProdutosVendaRepository itensVenda,
    IFormaPagtoRepository formasPagto,
    ICupomRepository cupons,
    ICalculoFreteService freteService,
    ITenantParcelamentoProvider parcelamentoProvider,
    ITransactionScopeFactory txFactory,
    IUnitOfWork uow) : IVendaService
{
    private const decimal PrecoMock = 100m;

    public async Task<VendaDto?> GetByIdAsync(GetVendaByIdQuery query, CancellationToken ct = default)
    {
        var venda = await vendas.GetAsync(query.TenantId, query.VendaId, ct);
        if (venda is null) return null;
        var itens = await itensVenda.ListByVendaAsync(venda.Id, ct);
        var forma = await formasPagto.GetAsync(venda.IdFormaPagto.Value, ct);
        return ToDto(venda, itens, forma?.Descricao ?? "?");
    }

    public async Task<PagedResult<VendaDto>> ListAsync(ListVendasQuery query, CancellationToken ct = default)
    {
        BranchId? unidade = query.IdUnidade is null ? null : BranchId.From(query.IdUnidade.Value);
        var (items, total) = await vendas.ListAsync(query.TenantId, query.IdCliente, unidade, query.Page, query.PageSize, ct);
        var dtos = new List<VendaDto>();
        foreach (var v in items)
        {
            var itens = await itensVenda.ListByVendaAsync(v.Id, ct);
            var forma = await formasPagto.GetAsync(v.IdFormaPagto.Value, ct);
            dtos.Add(ToDto(v, itens, forma?.Descricao ?? "?"));
        }
        return new PagedResult<VendaDto>(dtos, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<VendaDto> CriarVendaAsync(PedidoDto pedidoDto, Guid formaPagtoId, decimal frete, int parcelas, bool isPago, CancellationToken ct = default)
        => throw new NotSupportedException("Use FinalizarCompraAsync com Tenant/Cliente para garantir transação.");

    // Método principal de fechamento — cobre Etapas 4+5 transacionalmente com idempotência/concorrência
    public async Task<VendaDto> FinalizarCompraAsync(TenantId tenantId, Guid clienteId, BranchId? unidadeId, string cep, Guid formaPagtoId, int parcelas, CancellationToken ct = default)
    {
        await using var tx = await txFactory.BeginTransactionAsync(ct);

        var carrinho = await pedidos.GetCarrinhoAbertoAsync(tenantId, clienteId, unidadeId, ct);
        if (carrinho is null) throw new BusinessRuleViolationException("Carrinho não encontrado.");
        var itens = await itensPedido.ListByPedidoAsync(carrinho.Id, ct);
        if (itens.Count == 0) throw new BusinessRuleViolationException("Carrinho vazio.");

        // Idempotência: se já existe venda para este pedido, retorna a existente (evita duplo processamento)
        var existente = await vendas.GetByPedidoAsync(carrinho.Id, ct);
        if (existente is not null)
        {
            var itensExist = await itensVenda.ListByVendaAsync(existente.Id, ct);
            var formaExist = await formasPagto.GetAsync(existente.IdFormaPagto.Value, ct);
            await tx.CommitAsync(ct);
            return ToDto(existente, itensExist, formaExist?.Descricao ?? "?");
        }

        if (carrinho.Status != PedidoStatus.Aberto)
            throw new BusinessRuleViolationException("Pedido não está aberto para fechamento.");

        var forma = await formasPagto.GetAsync(formaPagtoId, ct);
        if (forma is null) throw new BusinessRuleViolationException("Forma de pagamento inválida.");

        // Valida parcelas
        var formasNaoParcelaveis = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Pix", "Transferência", "Transferencia", "Depósito", "Deposito", "Cartão de Débito", "Cartao de Debito" };
        var ehNaoParcelavel = formasNaoParcelaveis.Contains(forma.Descricao) || forma.QtdMaximaParcelas == 1;
        if (ehNaoParcelavel && parcelas != 1)
            throw new BusinessRuleViolationException($"{forma.Descricao} só permite 1 parcela.");
        var limiteTenant = await parcelamentoProvider.GetMaxParcelasAsync(tenantId, ct);
        var limiteEfetivo = limiteTenant is not null ? Math.Min(forma.QtdMaximaParcelas, limiteTenant.Value) : forma.QtdMaximaParcelas;
        if (parcelas > limiteEfetivo)
            throw new BusinessRuleViolationException($"Parcelas excede limite permitido ({limiteEfetivo}) para {forma.Descricao}.");

        // Calcula frete (mock)
        var freteValor = await freteService.CalcularAsync(cep, tenantId, unidadeId, itens.Select(i => (i.IdProduto.Value, i.Quantidade)).ToList(), ct);

        // Subtotal bruto e desconto (se cupom aplicado, carrinho.ValorTotal já é líquido)
        var subtotalBruto = itens.Sum(i => i.Quantidade * PrecoMock);
        var desconto = Math.Max(0, subtotalBruto - carrinho.ValorTotal);
        if (desconto > subtotalBruto) desconto = subtotalBruto;

        // Se cupom usado, decrementa quantidade (consumo)
        if (carrinho.IdCupom is not null)
        {
            var cupom = await cupons.GetAsync(tenantId, carrinho.IdCupom.Value.Value, ct);
            if (cupom is not null)
            {
                if (cupom.Quantidade <= 0) throw new BusinessRuleViolationException("Cupom sem usos disponíveis (concorrência).");
                cupom.DecrementarUso();
            }
        }

        // Cria Venda
        var venda = Venda.Criar(
            carrinho.Id,
            tenantId,
            FormaPagtoId.From(formaPagtoId),
            subtotalBruto,
            desconto,
            freteValor,
            isPago: false, // pagamento será confirmado externamente; inicia como false (AguardandoPagamento)
            parcelas,
            nrPedido: null);

        await vendas.AddAsync(venda, ct);

        // Copia itens
        var copias = itens.Select(i => ProdutosVenda.Create(venda.Id, i.IdProduto, i.Quantidade)).ToList();
        await itensVenda.AddRangeAsync(copias, ct);

        // Atualiza pedido
        carrinho.Fechar(PedidoStatus.VendaEfetuada);

        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Integração com Estoque (Etapa 5 — sugestão): baixa/reserva de estoque
        // Decisão: NÃO baixa automaticamente nesta versão; documentado como evolução futura
        // Motivo: estoque é por filial e exige validação de saldo + custo médio; criar dependência circular agora arrisca inconsistência.
        // Se fosse integrar: iterar itens e chamar IStockMovementService.SaidaAsync(...)

        var formaDesc = forma.Descricao;
        return ToDto(venda, copias, formaDesc);
    }

    private static VendaDto ToDto(Venda v, IReadOnlyList<ProdutosVenda> itens, string formaDesc) => new(
        v.Id.Value, v.IdPedido.Value, v.DataVenda, v.IdFormaPagto.Value, formaDesc,
        v.ValorBruto, v.ValorDesconto, v.ValorLiquidoPedido, v.ValorFrete, v.ValorFinal,
        v.IsPago, v.NrPedido, v.QuantidadeParcelar,
        itens.Select(i => new ProdutosVendaDto(i.Id.Value, i.IdProduto.Value, i.Quantidade)).ToList());
}
