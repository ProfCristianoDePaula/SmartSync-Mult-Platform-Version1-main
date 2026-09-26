using Fiscal.Application.Emissao;

namespace Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Tabela de cStat como DADO versionado (não hardcode espalhado).
/// Fonte: leiaute 4.00 — principais códigos; completar contra o manual
/// vigente antes de homologação real (PENDENCIAS.md F0-26).
/// HTTP 200 nunca implica autorização: só cStat de autorização autoriza.
/// </summary>
public static class CStatTabela
{
    public sealed record Entrada(string Codigo, string Significado, ResultadoTransmissao Efeito);

    private static readonly Dictionary<string, Entrada> Tabela = new()
    {
        ["100"] = new("100", "Autorizado o uso da NF-e", ResultadoTransmissao.Autorizado),
        ["101"] = new("101", "Cancelamento homologado", ResultadoTransmissao.Autorizado),
        ["102"] = new("102", "Inutilização homologada", ResultadoTransmissao.Autorizado),
        ["103"] = new("103", "Lote recebido com sucesso", ResultadoTransmissao.Aguardando),
        ["104"] = new("104", "Lote processado", ResultadoTransmissao.Aguardando),
        ["107"] = new("107", "Serviço em operação", ResultadoTransmissao.Autorizado),
        ["108"] = new("108", "Serviço paralisado momentaneamente", ResultadoTransmissao.ErroTecnico),
        ["109"] = new("109", "Serviço paralisado sem previsão", ResultadoTransmissao.ErroTecnico),
        ["105"] = new("105", "Lote em processamento", ResultadoTransmissao.Aguardando),
        ["110"] = new("110", "Uso denegado (histórico)", ResultadoTransmissao.Rejeitado),
        ["135"] = new("135", "Evento registrado e vinculado", ResultadoTransmissao.Autorizado),
        ["136"] = new("136", "Evento registrado sem vinculação", ResultadoTransmissao.Autorizado),
        ["155"] = new("155", "Cancelamento homologado fora do prazo? (conferir manual)", ResultadoTransmissao.Autorizado),
        ["201"] = new("201", "Rejeição: número máximo de numeração a inutilizar excedido (exemplo)", ResultadoTransmissao.Rejeitado),
        ["301"] = new("301", "Uso denegado: emitente irregular (histórico)", ResultadoTransmissao.Rejeitado),
        ["302"] = new("302", "Uso denegado: destinatário bloqueado (histórico)", ResultadoTransmissao.Rejeitado),
        ["303"] = new("303", "Uso denegado: destinatário sem inscrição (histórico)", ResultadoTransmissao.Rejeitado),
    };

    public static ResultadoTransmissao EfeitoDe(string? cStat)
    {
        if (cStat is not null && Tabela.TryGetValue(cStat.Trim(), out var entrada))
            return entrada.Efeito;
        return ResultadoTransmissao.Rejeitado;
    }

    public static string SignificadoDe(string? cStat)
        => cStat is not null && Tabela.TryGetValue(cStat.Trim(), out var entrada)
            ? entrada.Significado
            : $"Retorno {cStat} (tabela parcial — conferir manual vigente).";
}
