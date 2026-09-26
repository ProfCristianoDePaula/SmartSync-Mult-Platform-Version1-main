using Fiscal.Domain.Common;

namespace Fiscal.Domain.Entities;

/// <summary>Histórico de importações da lista de municípios (quem/quando/arquivo/totais).</summary>
public sealed class NfseImportRun : Entity<NfseImportRunId>
{
    public DateTime OcorridoEm { get; private set; }
    public Guid? UserId { get; private set; }
    public string ArquivoHash { get; private set; } = null!;
    public int Total { get; private set; }
    public int Criados { get; private set; }
    public int Atualizados { get; private set; }
    public int IgnoradosManuais { get; private set; }
    public int Rejeitados { get; private set; }

    private NfseImportRun() { }

    private NfseImportRun(
        NfseImportRunId id, Guid? userId, string arquivoHash,
        int total, int criados, int atualizados, int ignoradosManuais, int rejeitados)
        : base(id)
    {
        OcorridoEm = DateTime.UtcNow;
        UserId = userId;
        ArquivoHash = arquivoHash;
        Total = total;
        Criados = criados;
        Atualizados = atualizados;
        IgnoradosManuais = ignoradosManuais;
        Rejeitados = rejeitados;
    }

    public static NfseImportRun Create(
        Guid? userId, string arquivoHash,
        int total, int criados, int atualizados, int ignoradosManuais, int rejeitados)
        => new(NfseImportRunId.New(), userId, arquivoHash, total, criados, atualizados, ignoradosManuais, rejeitados);
}
