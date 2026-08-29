namespace Estoque.Domain.Enums;

/// <summary>
/// Tipo de pessoa de um fornecedor (espelho do padrão do Tenant — Etapa 17):
/// pessoa física (CPF) ou jurídica (CNPJ).
/// </summary>
public enum TipoPessoa
{
    Fisica = 1,
    Juridica = 2
}
