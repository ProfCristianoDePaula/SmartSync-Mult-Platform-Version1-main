using Estoque.Application.Common;
using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using FluentValidation;

namespace Estoque.Application.PedidosVendas;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record CupomDto(
    Guid Id,
    string Descricao,
    decimal ValorDesconto,
    decimal PercDesconto,
    decimal ValorMinimoCompra,
    Guid? IdCategoria,
    DateTime DataValidade,
    DateTime DataCriacao,
    int Quantidade,
    bool IsCupomProduto,
    IReadOnlyList<Guid> ProdutosIds);

public sealed record FormaPagtoDto(
    Guid Id,
    string Descricao,
    int QtdMaximaParcelas);

public sealed record PedidoDto(
    Guid Id,
    DateTime DataAbertura,
    Guid? IdCliente,
    Guid? IdCupom,
    Guid? IdUnidade,
    PedidoStatus Status,
    DateTime? DataFechamento,
    decimal ValorTotal,
    IReadOnlyList<ProdutosPedidoDto> Itens,
    decimal ValorDescontoAplicado,
    decimal ValorFrete);

public sealed record ProdutosPedidoDto(
    Guid Id,
    Guid IdProduto,
    decimal Quantidade,
    string ProdutoSku,
    string ProdutoNome);

public sealed record VendaDto(
    Guid Id,
    Guid IdPedido,
    DateTime DataVenda,
    Guid IdFormaPagto,
    string FormaPagtoDescricao,
    decimal ValorBruto,
    decimal ValorDesconto,
    decimal ValorLiquidoPedido,
    decimal ValorFrete,
    decimal ValorFinal,
    bool IsPago,
    string? NrPedido,
    int QuantidadeParcelar,
    IReadOnlyList<ProdutosVendaDto> Itens);

public sealed record ProdutosVendaDto(
    Guid Id,
    Guid IdProduto,
    decimal Quantidade);

public sealed record CarrinhoDto(
    PedidoDto Pedido,
    decimal SubtotalBruto,
    decimal DescontoCupom,
    decimal Frete,
    decimal TotalFinal);

public sealed record CalculoFreteDto(
    string Cep,
    decimal ValorFrete,
    string? Observacao);

public sealed record CheckoutResumoDto(
    Guid PedidoId,
    decimal ValorBruto,
    decimal ValorDesconto,
    decimal ValorFrete,
    decimal ValorFinal,
    FormaPagtoDto FormaPagto,
    int QuantidadeParcelar,
    bool IsPago);

// ---------------------------------------------------------------------------
// Cupom — Commands/Queries
// ---------------------------------------------------------------------------

public sealed record CreateCupomCommand(
    string Descricao,
    decimal ValorDesconto,
    decimal PercDesconto,
    decimal ValorMinimoCompra,
    Guid? IdCategoria,
    DateTime DataValidade,
    int Quantidade,
    bool IsCupomProduto,
    IReadOnlyList<Guid>? ProdutosIds = null)
{
    public CreateCupomCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpdateCupomCommand(
    Guid CupomId,
    string Descricao,
    decimal ValorDesconto,
    decimal PercDesconto,
    decimal ValorMinimoCompra,
    Guid? IdCategoria,
    DateTime DataValidade,
    int Quantidade,
    bool IsCupomProduto,
    IReadOnlyList<Guid>? ProdutosIds = null)
{
    public UpdateCupomCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record GetCupomByIdQuery(Guid CupomId)
{
    public GetCupomByIdQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record ListCuponsQuery(
    string? Search = null,
    bool? ApenasValidos = null,
    int Page = 1,
    int PageSize = 20)
{
    public ListCuponsQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record AplicarCupomCommand(
    Guid PedidoId,
    Guid CupomId)
{
    public AplicarCupomCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
    public AplicarCupomCommand WithCliente(Guid? clienteId) => this with { ClienteId = clienteId };
    public Guid? ClienteId { get; init; }
}

// Resultado da aplicação do cupom (para Etapa 3/4)
public sealed record AplicarCupomResult(
    bool Sucesso,
    decimal ValorDesconto,
    string? MotivoFalha,
    IReadOnlyList<Guid> ProdutosElegiveisIds);

// ---------------------------------------------------------------------------
// Pedido / Carrinho — Commands
// ---------------------------------------------------------------------------

public sealed record GetOrCreateCarrinhoCommand(
    Guid? IdUnidade)
{
    public GetOrCreateCarrinhoCommand WithContext(TenantId tenantId, Guid clienteId) => this with { TenantId = tenantId, ClienteId = clienteId };
    public TenantId TenantId { get; init; }
    public Guid ClienteId { get; init; }
}

public sealed record AdicionarItemCommand(
    Guid ProdutoId,
    decimal Quantidade,
    Guid? IdUnidade = null)
{
    public AdicionarItemCommand WithContext(TenantId tenantId, Guid clienteId) => this with { TenantId = tenantId, ClienteId = clienteId };
    public TenantId TenantId { get; init; }
    public Guid ClienteId { get; init; }
}

public sealed record RemoverItemCommand(
    Guid ProdutoId,
    Guid? IdUnidade = null)
{
    public RemoverItemCommand WithContext(TenantId tenantId, Guid clienteId) => this with { TenantId = tenantId, ClienteId = clienteId };
    public TenantId TenantId { get; init; }
    public Guid ClienteId { get; init; }
}

public sealed record AplicarCupomCarrinhoCommand(
    Guid CupomId,
    Guid? IdUnidade = null)
{
    public AplicarCupomCarrinhoCommand WithContext(TenantId tenantId, Guid clienteId) => this with { TenantId = tenantId, ClienteId = clienteId };
    public TenantId TenantId { get; init; }
    public Guid ClienteId { get; init; }
}

public sealed record ConsultarCarrinhoQuery(Guid? IdUnidade = null)
{
    public ConsultarCarrinhoQuery WithContext(TenantId tenantId, Guid clienteId) => this with { TenantId = tenantId, ClienteId = clienteId };
    public TenantId TenantId { get; init; }
    public Guid ClienteId { get; init; }
}

public sealed record ListPedidosQuery(
    Guid? IdCliente = null,
    Guid? IdUnidade = null,
    PedidoStatus? Status = null,
    int Page = 1,
    int PageSize = 20)
{
    public ListPedidosQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

// ---------------------------------------------------------------------------
// Checkout — Commands
// ---------------------------------------------------------------------------

public sealed record CalcularFreteCommand(
    string Cep,
    Guid? IdUnidade = null)
{
    public CalcularFreteCommand WithContext(TenantId tenantId, Guid clienteId) => this with { TenantId = tenantId, ClienteId = clienteId };
    public TenantId TenantId { get; init; }
    public Guid ClienteId { get; init; }
}

public sealed record FinalizarCheckoutCommand(
    string Cep,
    Guid IdFormaPagto,
    int QuantidadeParcelas,
    Guid? IdUnidade = null)
{
    public FinalizarCheckoutCommand WithContext(TenantId tenantId, Guid clienteId) => this with { TenantId = tenantId, ClienteId = clienteId };
    public TenantId TenantId { get; init; }
    public Guid ClienteId { get; init; }
}

// ---------------------------------------------------------------------------
// Venda — Queries
// ---------------------------------------------------------------------------

public sealed record GetVendaByIdQuery(Guid VendaId)
{
    public GetVendaByIdQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record ListVendasQuery(
    Guid? IdCliente = null,
    Guid? IdUnidade = null,
    int Page = 1,
    int PageSize = 20)
{
    public ListVendasQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

// ---------------------------------------------------------------------------
// Validators
// ---------------------------------------------------------------------------

public sealed class CreateCupomCommandValidator : AbstractValidator<CreateCupomCommand>
{
    public CreateCupomCommandValidator()
    {
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(255).WithMessage("Descrição é obrigatória (≤255).");
        RuleFor(x => x.ValorDesconto).GreaterThanOrEqualTo(0).WithMessage("ValorDesconto não pode ser negativo.");
        RuleFor(x => x.PercDesconto).InclusiveBetween(0, 100).WithMessage("PercDesconto deve estar entre 0 e 100.");
        RuleFor(x => x.ValorMinimoCompra).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DataValidade).NotEqual(default(DateTime)).Must(d => d.Date >= DateTime.UtcNow.Date).WithMessage("DataValidade não pode ser retroativa.");
        RuleFor(x => x.Quantidade).GreaterThanOrEqualTo(0).WithMessage("Quantidade não pode ser negativa.");
        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            var temValor = cmd.ValorDesconto > 0;
            var temPerc = cmd.PercDesconto > 0;
            if (temValor && temPerc) ctx.AddFailure("Informe apenas ValorDesconto OU PercDesconto, não ambos.");
            if (!temValor && !temPerc) ctx.AddFailure("Cupom deve ter ao menos um tipo de desconto.");
        });
        RuleFor(x => x.IdCategoria).Must(id => id == null || id != Guid.Empty).WithMessage("IdCategoria inválido.");
        When(x => x.IsCupomProduto, () =>
        {
            RuleFor(x => x.ProdutosIds).NotNull().Must(ids => ids!.Count > 0).WithMessage("Cupom de produto deve ter ao menos um produto vinculado.");
        });
    }
}

public sealed class UpdateCupomCommandValidator : AbstractValidator<UpdateCupomCommand>
{
    public UpdateCupomCommandValidator()
    {
        RuleFor(x => x.CupomId).NotEqual(Guid.Empty);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ValorDesconto).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PercDesconto).InclusiveBetween(0, 100);
        RuleFor(x => x.Quantidade).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            var temValor = cmd.ValorDesconto > 0;
            var temPerc = cmd.PercDesconto > 0;
            if (temValor && temPerc) ctx.AddFailure("Informe apenas ValorDesconto OU PercDesconto.");
            if (!temValor && !temPerc) ctx.AddFailure("Cupom deve ter desconto.");
        });
    }
}

public sealed class AdicionarItemCommandValidator : AbstractValidator<AdicionarItemCommand>
{
    public AdicionarItemCommandValidator()
    {
        RuleFor(x => x.ProdutoId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantidade).GreaterThan(0).WithMessage("Quantidade deve ser > 0.");
    }
}

public sealed class CalcularFreteCommandValidator : AbstractValidator<CalcularFreteCommand>
{
    public CalcularFreteCommandValidator()
    {
        RuleFor(x => x.Cep).NotEmpty().Must(cep => cep.Replace("-", "").Trim().Length == 8).WithMessage("CEP deve conter 8 dígitos.");
    }
}

public sealed class FinalizarCheckoutCommandValidator : AbstractValidator<FinalizarCheckoutCommand>
{
    public FinalizarCheckoutCommandValidator()
    {
        RuleFor(x => x.Cep).NotEmpty().Must(c => c.Replace("-", "").Trim().Length == 8).WithMessage("CEP inválido.");
        RuleFor(x => x.IdFormaPagto).NotEqual(Guid.Empty);
        RuleFor(x => x.QuantidadeParcelas).GreaterThan(0);
    }
}

public sealed class ListPedidosQueryValidator : AbstractValidator<ListPedidosQuery>
{
    public ListPedidosQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

// ---------------------------------------------------------------------------
// Service Interfaces (Application contracts)
// ---------------------------------------------------------------------------

public interface ICupomService
{
    Task<CupomDto> CreateAsync(CreateCupomCommand command, CancellationToken ct = default);
    Task<CupomDto?> UpdateAsync(UpdateCupomCommand command, CancellationToken ct = default);
    Task<bool> DeleteAsync(TenantId tenantId, Guid cupomId, CancellationToken ct = default);
    Task<CupomDto?> GetByIdAsync(GetCupomByIdQuery query, CancellationToken ct = default);
    Task<PagedResult<CupomDto>> ListAsync(ListCuponsQuery query, CancellationToken ct = default);
}

public interface IAplicarCupomService
{
    Task<AplicarCupomResult> AplicarAsync(AplicarCupomCommand command, CancellationToken ct = default);
    Task<decimal> CalcularDescontoAsync(TenantId tenantId, Guid cupomId, IReadOnlyList<(Guid ProdutoId, Guid? CategoriaId, decimal Quantidade, decimal PrecoUnitario)> itensCarrinho, CancellationToken ct = default);
}

public interface IPedidoService
{
    Task<PedidoDto> GetOrCreateCarrinhoAsync(GetOrCreateCarrinhoCommand command, CancellationToken ct = default);
    Task<PedidoDto> AdicionarItemAsync(AdicionarItemCommand command, CancellationToken ct = default);
    Task<PedidoDto> RemoverItemAsync(RemoverItemCommand command, CancellationToken ct = default);
    Task<PedidoDto> AplicarCupomAsync(AplicarCupomCarrinhoCommand command, CancellationToken ct = default);
    Task<PedidoDto?> ConsultarCarrinhoAsync(ConsultarCarrinhoQuery query, CancellationToken ct = default);
    Task<PagedResult<PedidoDto>> ListAsync(ListPedidosQuery query, CancellationToken ct = default);
    Task<PedidoDto?> GetByIdAsync(TenantId tenantId, Guid pedidoId, CancellationToken ct = default);
    Task<PedidoDto> CancelarAsync(TenantId tenantId, Guid pedidoId, CancellationToken ct = default);
}

public interface ICalculoFreteService
{
    Task<decimal> CalcularAsync(string cep, TenantId tenantId, BranchId? unidadeId, IReadOnlyList<(Guid ProdutoId, decimal Quantidade)> itens, CancellationToken ct = default);
}

public interface ICheckoutService
{
    Task<CalculoFreteDto> CalcularFreteAsync(CalcularFreteCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<FormaPagtoDto>> ListarFormasPagtoAsync(TenantId tenantId, CancellationToken ct = default);
    Task<VendaDto> FinalizarAsync(FinalizarCheckoutCommand command, CancellationToken ct = default);
}

public interface IVendaService
{
    Task<VendaDto?> GetByIdAsync(GetVendaByIdQuery query, CancellationToken ct = default);
    Task<PagedResult<VendaDto>> ListAsync(ListVendasQuery query, CancellationToken ct = default);
    Task<VendaDto> CriarVendaAsync(PedidoDto pedido, Guid formaPagtoId, decimal frete, int parcelas, bool isPago, CancellationToken ct = default);
    Task<VendaDto> FinalizarCompraAsync(TenantId tenantId, Guid clienteId, BranchId? unidadeId, string cep, Guid formaPagtoId, int parcelas, CancellationToken ct = default);
}

public interface IFormaPagtoService
{
    Task<IReadOnlyList<FormaPagtoDto>> ListAsync(CancellationToken ct = default);
    Task<FormaPagtoDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public interface ITenantParcelamentoProvider
{
    /// <summary>Retorna o limite máximo de parcelas configurado no Tenant/Loja, ou null se não houver (fallback para FormaPagto).</summary>
    Task<int?> GetMaxParcelasAsync(TenantId tenantId, CancellationToken ct = default);
}
