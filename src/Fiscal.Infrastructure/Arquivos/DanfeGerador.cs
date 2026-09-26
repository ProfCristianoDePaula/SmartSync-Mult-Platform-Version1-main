using System.Text.Json;
using Fiscal.Application.Arquivos;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;

namespace Fiscal.Infrastructure.Arquivos;

/// <summary>
/// DANFE/DANFCE simplificado (Fiscal-11): cabeçalho, emitente, destinatário,
/// número/série/chave/protocolo, valores e marcação de homologação
/// ("SEM VALOR FISCAL") quando o ambiente não é produção.
/// </summary>
public sealed class DanfeGerador : IDanfeGerador
{
    public byte[] Gerar(DocumentoFiscal doc)
    {
        // DANFSe: via geração local simplificada (decisão ADR — serviço oficial
        // como evolução; dados sempre do documento oficial).
        var titulo = doc.Tipo switch
        {
            TipoDocumentoFiscal.NFCe65 => "DANFCE SIMPLIFICADO - Cupom Fiscal Eletronico",
            TipoDocumentoFiscal.NFSe => "DANFSE SIMPLIFICADO - Documento Auxiliar da NFS-e",
            _ => "DANFE SIMPLIFICADO - Documento Auxiliar da NF-e"
        };
        var linhas = new List<string>();

        if (doc.Ambiente == AmbienteFiscal.Homologacao)
        {
            linhas.Add("*** SEM VALOR FISCAL - AMBIENTE DE HOMOLOGACAO ***");
            linhas.Add("");
        }

        var emit = LerObjeto(doc.SnapshotEmitente);
        linhas.Add($"Emitente: {Ler(emit, "RazaoSocial")} - CNPJ {Ler(emit, "Numero")}");
        linhas.Add($"Numero: {doc.Numero:D9}  Serie: {doc.Serie}  Modelo: {(int)doc.Tipo}");
        linhas.Add($"Chave: {doc.ChaveAcesso ?? "-"}");
        linhas.Add($"Protocolo: {doc.Protocolo ?? "-"}  cStat: {doc.CStat ?? "-"}");
        linhas.Add($"Emissao: {doc.CreatedAtUtc:yyyy-MM-dd HH:mm}Z  Status: {doc.Status}");
        linhas.Add("");
        linhas.Add("Destinatario: " + doc.SnapshotDestinatario);
        linhas.Add("");
        linhas.Add("Itens: " + doc.SnapshotItens);
        linhas.Add("");
        linhas.Add("Totais: " + doc.SnapshotTotais);
        linhas.Add("");
        linhas.Add("Consulte a autenticidade no portal do autorizador pela chave de acesso.");
        linhas.Add("O PDF nao substitui o XML fiscal autorizado.");

        return PdfSimples.Gerar(titulo, linhas);
    }

    private static JsonElement LerObjeto(string json)
    {
        try { return JsonDocument.Parse(json).RootElement; }
        catch { return JsonDocument.Parse("{}").RootElement; }
    }

    private static string Ler(JsonElement e, string nome)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var p) ? p.ToString() : "-";
}
