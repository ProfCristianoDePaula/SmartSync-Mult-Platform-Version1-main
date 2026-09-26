namespace Fiscal.Infrastructure.Catalogo;

/// <summary>
/// Opções do catálogo SEFAZ — seção "Fiscal:Sefaz".
/// Sufixos de host permitidos para URLs de endpoints (R8: só hosts oficiais).
/// </summary>
public sealed class SefazCatalogOptions
{
    public const string SectionName = "Fiscal:Sefaz";

    public List<string> AllowedHostSuffixes { get; set; } = [".gov.br"];
}
