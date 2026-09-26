namespace Fiscal.Application.Emitentes;

/// <summary>Opções do módulo Fiscal — seção "Fiscal". Produção desativada por padrão (R10).</summary>
public sealed class FiscalOptions
{
    public const string SectionName = "Fiscal";

    public bool ProducaoHabilitada { get; set; }
}
