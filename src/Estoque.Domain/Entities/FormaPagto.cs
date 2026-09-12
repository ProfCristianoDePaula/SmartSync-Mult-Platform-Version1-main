using Estoque.Domain.Common;

namespace Estoque.Domain.Entities;

/// <summary>
/// Forma de pagamento — tabela de domínio global (sem TenantId). Sem soft delete;
/// quantidade máxima de parcelas define a regra de parcelamento (Etapa 4).
/// Seeds em Etapa 6: Pix/Transferência/Depósito/Débito =1, Crédito=12.
/// </summary>
public sealed class FormaPagto : Entity<FormaPagtoId>
{
    public string Descricao { get; private set; } = null!;
    public int QtdMaximaParcelas { get; private set; }

    private FormaPagto() { }

    private FormaPagto(FormaPagtoId id, string descricao, int qtdMaximaParcelas)
        : base(id)
    {
        SetDescricao(descricao);
        if (qtdMaximaParcelas < 1)
            throw new ArgumentException("Quantidade máxima de parcelas deve ser ao menos 1.", nameof(qtdMaximaParcelas));
        if (qtdMaximaParcelas > 36)
            throw new ArgumentException("Quantidade máxima de parcelas excede o limite permitido (36).", nameof(qtdMaximaParcelas));
        QtdMaximaParcelas = qtdMaximaParcelas;
    }

    public static FormaPagto Create(string descricao, int qtdMaximaParcelas)
        => new(FormaPagtoId.New(), descricao, qtdMaximaParcelas);

    public static FormaPagto FromValidated(FormaPagtoId id, string descricao, int qtdMaximaParcelas)
        => new(id, descricao, qtdMaximaParcelas);

    public void Atualizar(string descricao, int qtdMaximaParcelas)
    {
        SetDescricao(descricao);
        if (qtdMaximaParcelas < 1)
            throw new ArgumentException("Quantidade máxima de parcelas deve ser ao menos 1.", nameof(qtdMaximaParcelas));
        QtdMaximaParcelas = qtdMaximaParcelas;
    }

    private void SetDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("Descrição da forma de pagamento é obrigatória.", nameof(descricao));
        if (descricao.Trim().Length > 100)
            throw new ArgumentException("Descrição da forma de pagamento excede 100 caracteres.", nameof(descricao));
        Descricao = descricao.Trim();
    }

    /// <summary>Formas não parceláveis (Etapa 4): Pix, Transferência, Depósito, Débito.</summary>
    public bool EhParcelavel => QtdMaximaParcelas > 1;
}
