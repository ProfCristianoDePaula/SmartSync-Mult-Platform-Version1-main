using Estoque.Domain.Common;
using Estoque.Domain.Enums;

namespace Estoque.Domain.Entities;

/// <summary>
/// Cupom de desconto — pode ser global (sem categoria/produtos), exclusivo por
/// categoria (IdCategoria preenchido) ou exclusivo por produtos (CupomProduto).
/// Pertence ao TENANT (TenantId obrigatório) para isolamento multi-tenant.
/// </summary>
public sealed class Cupom : Entity<CupomId>, IHasDomainEvents
{
    private readonly List<object> _events = [];

    public TenantId TenantId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal ValorDesconto { get; private set; }
    public decimal PercDesconto { get; private set; }
    public decimal ValorMinimoCompra { get; private set; }
    public CategoryId? IdCategoria { get; private set; }
    public DateTime DataValidade { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public int Quantidade { get; private set; }
    public bool IsCupomProduto { get; private set; }

    public IReadOnlyList<object> DomainEvents => _events;

    private Cupom() { }

    private Cupom(
        CupomId id,
        TenantId tenantId,
        string descricao,
        decimal valorDesconto,
        decimal percDesconto,
        decimal valorMinimoCompra,
        CategoryId? idCategoria,
        DateTime dataValidade,
        DateTime dataCriacao,
        int quantidade,
        bool isCupomProduto)
        : base(id)
    {
        TenantId = tenantId;
        SetDescricao(descricao);
        SetDesconto(valorDesconto, percDesconto);
        if (valorMinimoCompra < 0)
            throw new ArgumentException("Valor mínimo de compra não pode ser negativo.", nameof(valorMinimoCompra));
        ValorMinimoCompra = Math.Round(valorMinimoCompra, 2, MidpointRounding.ToEven);

        IdCategoria = idCategoria;

        if (dataValidade < dataCriacao)
            throw new BusinessRuleViolationException("Data de validade não pode ser anterior à data de criação.");

        DataValidade = dataValidade;
        DataCriacao = dataCriacao;

        if (quantidade < 0)
            throw new ArgumentException("Quantidade de usos não pode ser negativa.", nameof(quantidade));
        Quantidade = quantidade;
        IsCupomProduto = isCupomProduto;
    }

    public static Cupom Create(
        TenantId tenantId,
        string descricao,
        decimal valorDesconto,
        decimal percDesconto,
        decimal valorMinimoCompra,
        Guid? idCategoria,
        DateTime dataValidade,
        int quantidade,
        bool isCupomProduto)
    {
        var dataCriacao = DateTime.UtcNow;
        if (dataValidade < DateTime.UtcNow.Date)
            throw new BusinessRuleViolationException("Data de validade não pode ser retroativa.");

        return new Cupom(
            CupomId.New(),
            tenantId,
            descricao,
            valorDesconto,
            percDesconto,
            valorMinimoCompra,
            idCategoria is null ? null : CategoryId.From(idCategoria.Value),
            dataValidade,
            dataCriacao,
            quantidade,
            isCupomProduto);
    }

    /// <summary>
    /// Reconstrução para leitura/EF sem validar retroatividade (dados legados).
    /// </summary>
    public static Cupom FromValidated(
        CupomId id,
        TenantId tenantId,
        string descricao,
        decimal valorDesconto,
        decimal percDesconto,
        decimal valorMinimoCompra,
        CategoryId? idCategoria,
        DateTime dataValidade,
        DateTime dataCriacao,
        int quantidade,
        bool isCupomProduto)
        => new(id, tenantId, descricao, valorDesconto, percDesconto, valorMinimoCompra, idCategoria, dataValidade, dataCriacao, quantidade, isCupomProduto);

    public void Atualizar(
        string descricao,
        decimal valorDesconto,
        decimal percDesconto,
        decimal valorMinimoCompra,
        Guid? idCategoria,
        DateTime dataValidade,
        int quantidade,
        bool isCupomProduto)
    {
        SetDescricao(descricao);
        SetDesconto(valorDesconto, percDesconto);
        if (valorMinimoCompra < 0)
            throw new ArgumentException("Valor mínimo de compra não pode ser negativo.", nameof(valorMinimoCompra));
        ValorMinimoCompra = Math.Round(valorMinimoCompra, 2, MidpointRounding.ToEven);
        IdCategoria = idCategoria is null ? null : CategoryId.From(idCategoria.Value);
        if (dataValidade < DataCriacao)
            throw new BusinessRuleViolationException("Data de validade não pode ser anterior à data de criação.");
        DataValidade = dataValidade;
        if (quantidade < 0)
            throw new ArgumentException("Quantidade de usos não pode ser negativa.", nameof(quantidade));
        Quantidade = quantidade;
        IsCupomProduto = isCupomProduto;
    }

    public bool EstaValido => DateTime.UtcNow <= DataValidade && Quantidade > 0;
    public bool EstaExpirado => DateTime.UtcNow > DataValidade;

    public void DecrementarUso()
    {
        if (Quantidade <= 0)
            throw new BusinessRuleViolationException("Cupom sem usos disponíveis.");
        Quantidade--;
    }

    private void SetDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("Descrição do cupom é obrigatória.", nameof(descricao));
        if (descricao.Trim().Length > 255)
            throw new ArgumentException("Descrição do cupom excede 255 caracteres.", nameof(descricao));
        Descricao = descricao.Trim();
    }

    private void SetDesconto(decimal valorDesconto, decimal percDesconto)
    {
        valorDesconto = Math.Round(valorDesconto, 2, MidpointRounding.ToEven);
        percDesconto = Math.Round(percDesconto, 2, MidpointRounding.ToEven);

        if (valorDesconto < 0)
            throw new ArgumentException("Valor de desconto não pode ser negativo.", nameof(valorDesconto));
        if (percDesconto is < 0 or > 100)
            throw new ArgumentException("Percentual de desconto deve estar entre 0 e 100.", nameof(percDesconto));

        // Regra de desempate (decisão Etapa 1): usar UM ou outro, não ambos simultaneamente.
        // Isso evita ambiguidade de cálculo (cumulativo vs exclusivo). Se ambos forem
        // informados >0, considera-se inválido — o serviço de cupom documenta essa regra.
        if (valorDesconto > 0 && percDesconto > 0)
            throw new BusinessRuleViolationException(
                "Informe apenas ValorDesconto OU PercDesconto, não ambos simultaneamente.");
        if (valorDesconto == 0 && percDesconto == 0)
            throw new BusinessRuleViolationException(
                "Cupom deve ter ao menos um tipo de desconto (ValorDesconto ou PercDesconto).");

        ValorDesconto = valorDesconto;
        PercDesconto = percDesconto;
    }

    public void Raise(object domainEvent) => _events.Add(domainEvent);
    public IReadOnlyList<object> PopEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}
