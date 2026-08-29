namespace Estoque.Domain.Common;

/// <summary>
/// Constantes das claims do JWT emitido pelo microsserviço Identity.
/// COPIA de contrato — os valores DEVEM permanecer idênticos aos de
/// Identity.Domain.Common.JwtClaims (fonte da verdade). Alterar lá exige
/// espelhar aqui (e vice-versa), pois os serviços se comunicam por token.
/// </summary>
public static class JwtClaims
{
    public const string UserId = "user_id";
    public const string TenantId = "tenant_id";
    public const string Role = "role";
    public const string FullName = "full_name";
    public const string Email = "email";
    public const string EmailConfirmed = "email_confirmed";
    public const string PhoneConfirmed = "phone_confirmed";
    public const string ProfileComplete = "profile_complete";
}

/// <summary>
/// Roles da plataforma — cópia de contrato de Identity.Domain.Common.Roles.
/// O Estoque não cria roles; apenas consome as existentes.
/// </summary>
public static class PlatformRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Manager = "Manager";
    public const string Seller = "Seller";
    public const string Delivery = "Delivery";
    public const string Client = "Client";
}
