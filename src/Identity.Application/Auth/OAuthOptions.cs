namespace Identity.Application.Auth;

/// <summary>
/// Credenciais dos provedores OAuth (Google/Facebook). Vinculada à seção "OAuth"
/// do appsettings. As credenciais são criadas manualmente nos consoles oficiais
/// (Google Cloud / Facebook for Developers) e NUNCA commitadas.
/// </summary>
public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    public GoogleOptions Google { get; set; } = new();
    public FacebookOptions Facebook { get; set; } = new();

    public sealed class GoogleOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }

    public sealed class FacebookOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}