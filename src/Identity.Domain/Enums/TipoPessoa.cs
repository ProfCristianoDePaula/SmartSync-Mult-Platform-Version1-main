namespace Identity.Domain.Enums;

/// <summary>
/// Tipo de pessoa de um tenant (Etapa 17): tenants podem ser pessoa física (CPF)
/// ou jurídica (CNPJ), com documento único global independente do tipo.
/// </summary>
public enum TipoPessoa
{
    Fisica = 1,
    Juridica = 2
}
