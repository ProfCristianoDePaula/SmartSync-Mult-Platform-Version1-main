using Estoque.Application.Common;
using Estoque.Application.PedidosVendas;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using FluentValidation;

namespace Estoque.Infrastructure.Services.PedidosVendas;

public sealed class CupomService(
    ICupomRepository cupons,
    ICupomProdutoRepository cupomProdutos,
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork uow) : ICupomService
{
    public async Task<CupomDto> CreateAsync(CreateCupomCommand command, CancellationToken ct = default)
    {
        // FluentValidation is called in controller; double-check domain invariants
        // Valida categoria existe se informada
        CategoryId? catId = null;
        if (command.IdCategoria is not null)
        {
            var cat = await categories.GetAsync(command.TenantId, command.IdCategoria.Value, ct);
            if (cat is null || !cat.IsActive)
                throw new BusinessRuleViolationException("Categoria não encontrada.");
            catId = CategoryId.From(command.IdCategoria.Value);
        }

        // Produtos
        if (command.IsCupomProduto)
        {
            if (command.ProdutosIds is null || command.ProdutosIds.Count == 0)
                throw new BusinessRuleViolationException("Cupom de produto deve ter ao menos um produto vinculado.");
            foreach (var pid in command.ProdutosIds)
            {
                var prod = await products.GetAsync(command.TenantId, pid, ct);
                if (prod is null || !prod.IsActive)
                    throw new BusinessRuleViolationException($"Produto {pid} não encontrado.");
            }
        }
        else
        {
            if (command.ProdutosIds is not null && command.ProdutosIds.Count > 0)
                throw new BusinessRuleViolationException("Cupom global/categoria não deve ter produtos específicos.");
        }

        var cupom = Cupom.Create(
            command.TenantId,
            command.Descricao,
            command.ValorDesconto,
            command.PercDesconto,
            command.ValorMinimoCompra,
            command.IdCategoria,
            command.DataValidade,
            command.Quantidade,
            command.IsCupomProduto);

        await cupons.AddAsync(cupom, ct);
        await uow.SaveChangesAsync(ct);

        if (command.IsCupomProduto && command.ProdutosIds is not null)
        {
            var vinculos = command.ProdutosIds.Select(pid =>
                CupomProduto.Create(cupom.Id, ProductId.From(pid))).ToList();
            await cupomProdutos.AddRangeAsync(vinculos, ct);
            await uow.SaveChangesAsync(ct);
        }

        var produtosIds = command.IsCupomProduto ? command.ProdutosIds!.ToList() : new List<Guid>();
        return ToDto(cupom, produtosIds);
    }

    public async Task<CupomDto?> UpdateAsync(UpdateCupomCommand command, CancellationToken ct = default)
    {
        var cupom = await cupons.GetAsync(command.TenantId, command.CupomId, ct);
        if (cupom is null) return null;

        CategoryId? catId = null;
        if (command.IdCategoria is not null)
        {
            var cat = await categories.GetAsync(command.TenantId, command.IdCategoria.Value, ct);
            if (cat is null) throw new BusinessRuleViolationException("Categoria não encontrada.");
            catId = CategoryId.From(command.IdCategoria.Value);
        }

        cupom.Atualizar(
            command.Descricao,
            command.ValorDesconto,
            command.PercDesconto,
            command.ValorMinimoCompra,
            command.IdCategoria,
            command.DataValidade,
            command.Quantidade,
            command.IsCupomProduto);

        await uow.SaveChangesAsync(ct);

        // Atualiza vínculos
        await cupomProdutos.RemoveByCupomAsync(cupom.Id, ct);
        if (command.IsCupomProduto && command.ProdutosIds is not null && command.ProdutosIds.Count > 0)
        {
            var vinculos = command.ProdutosIds.Select(pid => CupomProduto.Create(cupom.Id, ProductId.From(pid)));
            await cupomProdutos.AddRangeAsync(vinculos, ct);
            await uow.SaveChangesAsync(ct);
        }
        else
        {
            await uow.SaveChangesAsync(ct);
        }

        var produtos = await cupomProdutos.ListByCupomAsync(cupom.Id, ct);
        return ToDto(cupom, produtos.Select(p => p.IdProduto.Value).ToList());
    }

    public async Task<bool> DeleteAsync(TenantId tenantId, Guid cupomId, CancellationToken ct = default)
    {
        var cupom = await cupons.GetAsync(tenantId, cupomId, ct);
        if (cupom is null) return false;
        await cupomProdutos.RemoveByCupomAsync(cupom.Id, ct);
        cupons.Remove(cupom);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CupomDto?> GetByIdAsync(GetCupomByIdQuery query, CancellationToken ct = default)
    {
        var cupom = await cupons.GetAsync(query.TenantId, query.CupomId, ct);
        if (cupom is null) return null;
        var produtos = await cupomProdutos.ListByCupomAsync(cupom.Id, ct);
        return ToDto(cupom, produtos.Select(p => p.IdProduto.Value).ToList());
    }

    public async Task<PagedResult<CupomDto>> ListAsync(ListCuponsQuery query, CancellationToken ct = default)
    {
        var (items, total) = await cupons.ListAsync(query.TenantId, query.Search, query.ApenasValidos, query.Page, query.PageSize, ct);
        var dtos = new List<CupomDto>();
        foreach (var c in items)
        {
            var prods = await cupomProdutos.ListByCupomAsync(c.Id, ct);
            dtos.Add(ToDto(c, prods.Select(p => p.IdProduto.Value).ToList()));
        }
        return new PagedResult<CupomDto>(dtos, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    private static CupomDto ToDto(Cupom c, IReadOnlyList<Guid> produtosIds) => new(
        c.Id.Value, c.Descricao, c.ValorDesconto, c.PercDesconto, c.ValorMinimoCompra,
        c.IdCategoria?.Value, c.DataValidade, c.DataCriacao, c.Quantidade, c.IsCupomProduto, produtosIds);
}
