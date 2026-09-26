using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Sequência por (emitente, modelo, série, ambiente). O incremento é ATÔMICO
/// no banco (`UPDATE ... RETURNING` em transação) — nunca `max()+1` (R7).
/// Sem soft delete: histórico de numeração preservado.
/// </summary>
public sealed class SerieNumeracao : Entity<SerieNumeracaoId>
{
    public EmitenteFiscalId EmitenteId { get; private set; }
    public int Modelo { get; private set; }
    public string Serie { get; private set; } = null!;
    public AmbienteFiscal Ambiente { get; private set; }
    public int UltimoNumero { get; private set; }

    private SerieNumeracao() { }

    private SerieNumeracao(SerieNumeracaoId id, EmitenteFiscalId emitenteId, int modelo, string serie, AmbienteFiscal ambiente)
        : base(id)
    {
        EmitenteId = emitenteId;
        Modelo = modelo;
        Serie = serie;
        Ambiente = ambiente;
        UltimoNumero = 0;
    }

    public static SerieNumeracao Create(EmitenteFiscalId emitenteId, int modelo, string serie, AmbienteFiscal ambiente)
    {
        if (modelo is not (55 or 65 or 200)) throw new ArgumentException("Modelo deve ser 55, 65 ou 200 (NFS-e).", nameof(modelo));
        if (string.IsNullOrWhiteSpace(serie)) throw new ArgumentException("Série obrigatória.", nameof(serie));
        return new(SerieNumeracaoId.New(), emitenteId, modelo, serie.Trim(), ambiente);
    }
}
