namespace Identity.Domain.Common;

/// <summary>
/// Constantes de claims usadas no JWT e para leitura pelo próprio serviço.
/// Padroniza o contrato entre emissor (aqui) e validadores (microsserviços).
/// </summary>
public static class JwtClaims
{
    public const string UserId = "user_id";
    public const string TenantId = "tenant_id";
    public const string Role = "role";
    public const string FullName = "full_name";
    public const string Email = "email";

    /// <summary>"true"/"false" do Identity (UserManager/Password). Usada na policy
    /// de acesso a a��es que exigem conta ativa.</summary>
    public const string EmailConfirmed = "email_confirmed";
    public const string PhoneConfirmed = "phone_confirmed";

    /// <summary>"true"/"false" — cadastro do Client completo (documento + nome
    /// preenchidos). Permite o frontend rotear para a etapa de onboarding.</summary>
    public const string ProfileComplete = "profile_complete";
}