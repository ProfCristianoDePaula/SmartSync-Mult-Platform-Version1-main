using Identity.Domain.Enums;

namespace Identity.Application.Tenants;

/// <summary>
/// Dados para cadastrar um novo tenant. O tenant pode ser pessoa física (CPF)
/// ou jurídica (CNPJ) — ver <see cref="TipoPessoa"/>. A contratação de
/// módulos/planos é feita DEPOIS, via endpoints de vínculo (Etapa 15) — o tenant
/// nasce sem módulos e os adquire pela rota <c>api/tenants/{id}/modules</c>.
/// </summary>
public sealed record CreateTenantCommand(
    string LegalName,
    string TradeName,
    TipoPessoa TipoPessoa,
    string Documento,
    string Email);
