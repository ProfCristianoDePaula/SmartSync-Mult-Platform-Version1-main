namespace Fiscal.Infrastructure.Arquivos;

/// <summary>Opções do armazenamento — seção "Fiscal:Armazenamento".</summary>
public sealed class ArmazenamentoOptions
{
    public const string SectionName = "Fiscal:Armazenamento";

    /// <summary>Raiz dos arquivos (absoluto ou relativo ao content root).</summary>
    public string Raiz { get; set; } = "fiscal-arquivos";

    /// <summary>Retenção mínima em dias. 0 = nunca apagar (padrão, R9).</summary>
    public int RetencaoDias { get; set; }
}
