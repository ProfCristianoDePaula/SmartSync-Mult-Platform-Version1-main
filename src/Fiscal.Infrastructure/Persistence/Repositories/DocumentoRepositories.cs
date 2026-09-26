using Fiscal.Application.Documentos;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fiscal.Infrastructure.Persistence.Repositories;

public sealed class DocumentoFiscalRepository(FiscalDbContext db) : IDocumentoFiscalRepository
{
    public Task<DocumentoFiscal?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
        => db.DocumentosFiscais.FirstOrDefaultAsync(d =>
            d.TenantId == tenantId && d.Id == DocumentoFiscalId.From(id), ct);

    public Task<DocumentoFiscal?> GetByIdempotencyAsync(TenantId tenantId, string key, CancellationToken ct = default)
        => db.DocumentosFiscais.FirstOrDefaultAsync(d =>
            d.TenantId == tenantId && d.IdempotencyKey == key.Trim(), ct);

    public async Task<IReadOnlyList<DocumentoFiscal>> ListByVendaAsync(TenantId tenantId, Guid vendaId, CancellationToken ct = default)
        => await db.DocumentosFiscais.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.OrigemVendaId == vendaId)
            .OrderBy(d => d.CreatedAtUtc).ToListAsync(ct);

    public async Task AddAsync(DocumentoFiscal documento, CancellationToken ct = default)
        => await db.DocumentosFiscais.AddAsync(documento, ct);
}

public sealed class SerieNumeracaoService(FiscalDbContext db) : ISerieNumeracaoService
{
    public async Task<int> ReservarAsync(
        EmitenteFiscalId emitenteId, int modelo, string serie, AmbienteFiscal ambiente,
        CancellationToken ct = default)
    {
        // Reserva atômica (R7) em UMA instrução: cria a série (1) ou incrementa.
        // Concorrentes bloqueiam na linha (row lock) — sem max()+1, sem
        // SELECT+INSERT (a segunda tentativa concorrente cai no ON CONFLICT).
        // Roda na transação ambiente do chamador, se houver.
        if (modelo is not (55 or 65 or 200))
            throw new ArgumentException("Modelo deve ser 55, 65 ou 200 (NFS-e).", nameof(modelo));
        serie = serie.Trim();

        var conn = db.Database.GetDbConnection();
        var wasClosed = conn.State == System.Data.ConnectionState.Closed;
        if (wasClosed) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = """
                INSERT INTO series_numeracao (id, emitente_id, modelo, serie, ambiente, ultimo_numero)
                VALUES (@id, @emit, @modelo, @serie, @amb, 1)
                ON CONFLICT (emitente_id, modelo, serie, ambiente) DO UPDATE
                   SET ultimo_numero = series_numeracao.ultimo_numero + 1
                RETURNING ultimo_numero
                """;
            AddParam(cmd, "@id", Guid.NewGuid());
            AddParam(cmd, "@emit", emitenteId.Value);
            AddParam(cmd, "@modelo", modelo);
            AddParam(cmd, "@serie", serie);
            AddParam(cmd, "@amb", (int)ambiente);

            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }
        finally
        {
            if (wasClosed) await conn.CloseAsync();
        }
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
