namespace Estoque.Infrastructure.FiscalBridge;

/// <summary>Base do Fiscal para chamadas autenticadas — seção "Fiscal".</summary>
public sealed class FiscalClientOptions
{
    public const string SectionName = "Fiscal";

    public string BaseUrl { get; set; } = "http://fiscal-api:8080";
}
