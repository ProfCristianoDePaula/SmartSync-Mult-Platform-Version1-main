namespace Fiscal.Infrastructure.Xml;

/// <summary>
/// Obrigatoriedade do grupo IBS/CBS em tabela de dados versionada (não em
/// if espalhado). Fonte: NT 2025.002 v1.51 (Ato Conjunto RFB/CGIBS nº 1/2026),
/// conforme roteiro §3 — <b>reconfirmar texto vigente antes de homologação</b>
/// (PENDENCIAS.md F0-02). Regras de desobrigação Simples/MEI: confirmar.
/// </summary>
public static class IbsCbsRegras
{
    public const string NtVersao = "NT 2025.002 v1.51";
    public static readonly DateOnly VigenciaFatosGeradores = new(2026, 8, 3);

    /// <summary>
    /// Exigido para Regime Normal; Simples/MEI seguem desobrigação a confirmar.
    /// Ausência de rejeição (1115 futura) NÃO é dispensa.
    /// </summary>
    public static bool Exigido(int crt) => crt == 3;
}
