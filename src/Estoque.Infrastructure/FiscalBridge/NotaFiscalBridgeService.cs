using Estoque.Application.FiscalBridge;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Estoque.Infrastructure.FiscalBridge;

/// <summary>
/// Ponte Vendas→Fiscal (Fiscal-13, aditiva): monta o pedido de emissão a
/// partir de Venda/ProdutosVenda/cliente/frete/desconto e chama o Fiscal
/// repassando o JWT. Venda mista gera documentos separados (NF-e + NFS-e).
/// Sem baixa de estoque aqui (pendência já registrada no Estoque).
/// </summary>
public sealed class NotaFiscalBridgeService(
    IVendaRepository vendas,
    IProdutosVendaRepository itensVenda,
    IPedidoRepository pedidos,
    IProductRepository produtos,
    IFormaPagtoRepository formasPagto,
    IFiscalBridgeClient fiscal,
    ILogger<NotaFiscalBridgeService> logger) : INotaFiscalBridgeService
{
    public async Task<NotaFiscalVendaDto> EmitirNotaAsync(
        Guid vendaId, EmitirNotaRequest request, CancellationToken ct = default)
    {
        var tenantId = request.TenantId;
        var venda = await vendas.GetAsync(tenantId, vendaId, ct)
            ?? throw new BusinessRuleViolationException("Venda não encontrada.");
        var itens = await itensVenda.ListByVendaAsync(venda.Id, ct);
        if (itens.Count == 0)
            throw new BusinessRuleViolationException("Venda sem itens.");

        var pedido = await pedidos.GetAsync(tenantId, venda.IdPedido.Value, ct);
        if (pedido?.IdUnidade is null)
            throw new BusinessRuleViolationException(
                "Emissão bloqueada: venda sem unidade/filial determinada.");

        var emitente = await fiscal.ObterEmitentePorFilialAsync(tenantId, pedido.IdUnidade.Value.Value, ct)
            ?? throw new BusinessRuleViolationException("Filial sem emitente fiscal configurado.");

        var forma = await formasPagto.GetAsync(venda.IdFormaPagto.Value, ct);
        var tpag = FormaPagtoTpags.Para(forma?.Descricao ?? "");

        // Perfis fiscais (Fiscal-6) por item — sem perfil, 422 com pendências.
        var pendencias = await fiscal.ObterPendenciasProdutosAsync(
            tenantId, itens.Select(i => i.IdProduto.Value).ToList(), ct);

        var mercadorias = new List<Guid>();
        var servicos = new List<Guid>();
        var semPerfil = new List<string>();
        foreach (var item in itens)
        {
            var perfil = pendencias.FirstOrDefault(p => p.ProdutoId == item.IdProduto.Value);
            if (perfil is null)
            {
                var prod = await produtos.GetAsync(tenantId, item.IdProduto.Value, ct);
                semPerfil.Add($"{prod?.Sku.Value ?? item.IdProduto.Value.ToString()} (sem perfil fiscal)");
                continue;
            }
            if (perfil.Tipo == 2) servicos.Add(item.IdProduto.Value);
            else mercadorias.Add(item.IdProduto.Value);
        }

        if (semPerfil.Count > 0)
            throw new BusinessRuleViolationException(
                "Produtos sem perfil fiscal: " + string.Join("; ", semPerfil));

        var documentos = new List<DocumentoEmitidoDto>();
        var sugestoes = new List<string>();
        if (mercadorias.Count > 0 && request.Tipo is TipoDocumentoFiscalBridge.NFe55 or TipoDocumentoFiscalBridge.NFCe65)
            documentos.Add(await CriarDocumentoAsync(tenantId, venda, pedido, itens
                .Where(i => mercadorias.Contains(i.IdProduto.Value)).ToList(),
                emitente.Id, (int)request.Tipo, tpag, ct));
        if (servicos.Count > 0 && request.Tipo == TipoDocumentoFiscalBridge.NFSe200)
            documentos.Add(await CriarDocumentoAsync(tenantId, venda, pedido, itens
                .Where(i => servicos.Contains(i.IdProduto.Value)).ToList(),
                emitente.Id, 200, tpag, ct));
        if (documentos.Count == 0)
            throw new BusinessRuleViolationException(
                "Nenhum item compatível com o tipo de documento solicitado.");
        if (servicos.Count > 0 && request.Tipo is not TipoDocumentoFiscalBridge.NFSe200)
            sugestoes.Add($"Venda mista: {servicos.Count} item(ns) de serviço pendentes — emita a NFS-e em seguida.");
        if (mercadorias.Count > 0 && request.Tipo == TipoDocumentoFiscalBridge.NFSe200)
            sugestoes.Add($"Venda mista: {mercadorias.Count} item(ns) de mercadoria pendentes — emita a NF-e em seguida.");

        var resposta = MontarResposta(venda.Id.Value, documentos, pedido);
        return resposta with { AcoesSugeridas = [.. resposta.AcoesSugeridas, .. sugestoes] };
    }

    public async Task<NotaFiscalVendaDto> ConsultarAsync(Guid vendaId, TenantId tenantId, CancellationToken ct = default)
    {
        var venda = await vendas.GetAsync(tenantId, vendaId, ct)
            ?? throw new BusinessRuleViolationException("Venda não encontrada.");
        var docs = await fiscal.ListarPorVendaAsync(tenantId, vendaId, ct);
        var pedido = await pedidos.GetAsync(tenantId, venda.IdPedido.Value, ct);
        return MontarResposta(vendaId, docs.Select(d =>
            new DocumentoEmitidoDto(d.Id, d.Tipo, d.Status, d.Serie, d.Numero,
                d.ChaveAcesso, d.Protocolo, false)).ToList(), pedido);
    }

    public async Task AvaliarEmissaoAutomaticaAsync(Guid vendaId, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var venda = await vendas.GetAsync(tenantId, vendaId, ct);
            if (venda is null) return;
            var pedido = await pedidos.GetAsync(tenantId, venda.IdPedido.Value, ct);
            if (pedido?.IdUnidade is null) return;

            var emitente = await fiscal.ObterEmitentePorFilialAsync(tenantId, pedido.IdUnidade.Value.Value, ct);
            if (emitente is not { EmissaoAutomaticaVenda: true }) return;

            var req = new EmitirNotaRequest(TipoDocumentoFiscalBridge.NFe55)
                .WithContext(tenantId, pedido.IdCliente ?? Guid.Empty);
            await EmitirNotaAsync(vendaId, req, ct);
        }
        catch (Exception ex)
        {
            // Auto-emissão nunca quebra a venda (fonte da verdade comercial).
            logger.LogWarning(ex, "Auto-emissão falhou para venda {VendaId}; use emitir-nota manual.", vendaId);
        }
    }

    private async Task<DocumentoEmitidoDto> CriarDocumentoAsync(
        TenantId tenantId,
        Domain.Entities.Venda venda,
        Domain.Entities.Pedido? pedido,
        List<Domain.Entities.ProdutosVenda> itens,
        Guid emitenteId,
        int tipo,
        string tpag,
        CancellationToken ct)
    {
        var snapshotItens = new List<object>();
        foreach (var item in itens)
        {
            var prod = await produtos.GetAsync(tenantId, item.IdProduto.Value, ct);
            snapshotItens.Add(new
            {
                produtoId = item.IdProduto.Value,
                codigo = prod?.Sku.Value ?? "?",
                descricao = prod?.Name ?? "?",
                qtd = item.Quantidade,
                vu = 0m, // preço real por item: pendência F0-11/P5 do Estoque
                unCom = "UN"
            });
        }

        var body = new
        {
            emitenteId,
            tipo,
            origemVendaId = venda.Id.Value,
            origemPedidoId = venda.IdPedido.Value,
            snapshotDestinatario = System.Text.Json.JsonSerializer.Serialize(new
            {
                clienteId = pedido?.IdCliente,
                nome = "Cliente da venda",
            }),
            snapshotItens = System.Text.Json.JsonSerializer.Serialize(snapshotItens),
            snapshotTotais = System.Text.Json.JsonSerializer.Serialize(new
            {
                bruto = venda.ValorBruto,
                frete = venda.ValorFrete,
                desconto = venda.ValorDesconto,
                final = venda.ValorFinal,
                tpag
            }),
            observacaoPedido = (string?)null
        };

        var doc = await fiscal.CriarDocumentoAsync(
            tenantId, body, $"{venda.Id.Value}:{tipo}", ct);

        return new DocumentoEmitidoDto(doc.Id, doc.Tipo, doc.Status, doc.Serie,
            doc.Numero, doc.ChaveAcesso, doc.Protocolo, doc.CriadoAgora);
    }

    private static NotaFiscalVendaDto MontarResposta(
        Guid vendaId,
        List<DocumentoEmitidoDto> documentos,
        Domain.Entities.Pedido? pedido)
    {
        var sugestoes = new List<string>();
        if (pedido?.Status == Domain.Enums.PedidoStatus.Cancelado
            && documentos.Any(d => d.Status == 7))
            sugestoes.Add("Venda cancelada com nota autorizada: solicite o cancelamento fiscal dentro do prazo — o cancelamento da venda NÃO cancela a nota automaticamente.");

        return new NotaFiscalVendaDto(vendaId, documentos, sugestoes);
    }
}
