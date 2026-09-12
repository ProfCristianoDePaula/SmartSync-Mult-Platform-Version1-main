using Estoque.Application.PedidosVendas;
using Estoque.Application.Repositories;

namespace Estoque.Infrastructure.Services.PedidosVendas;

public sealed class FormaPagtoService(IFormaPagtoRepository repo) : IFormaPagtoService
{
    public async Task<IReadOnlyList<FormaPagtoDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await repo.ListAsync(ct);
        return items.Select(f => new FormaPagtoDto(f.Id.Value, f.Descricao, f.QtdMaximaParcelas)).ToList();
    }

    public async Task<FormaPagtoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = await repo.GetAsync(id, ct);
        return f is null ? null : new FormaPagtoDto(f.Id.Value, f.Descricao, f.QtdMaximaParcelas);
    }
}
