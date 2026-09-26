using Fiscal.Application.Auditing;
using Fiscal.Application.Catalogo;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using FluentValidation;

namespace Fiscal.Infrastructure.Catalogo;

public sealed class UfFiscalService(
    IUfFiscalRepository ufs,
    ISefazEndpointRepository endpoints,
    FiscalDbContext db,
    IAuditLogger audit,
    IValidator<CreateUfCommand> createValidator,
    IValidator<UpdateUfCommand> updateValidator) : IUfFiscalService
{
    public async Task<UfFiscalDto> CreateAsync(CreateUfCommand command, Guid userId, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(command, ct);

        var sigla = command.Sigla.Trim().ToUpperInvariant();

        if (await ufs.GetBySiglaAsync(sigla, ct) is not null)
            throw new BusinessRuleViolationException($"UF '{sigla}' já cadastrada.");

        if (await ufs.GetByCodigoIbgeAsync(command.CodigoIbge, ct) is not null)
            throw new BusinessRuleViolationException($"cUF {command.CodigoIbge} já cadastrado.");

        var uf = UfFiscal.Create(sigla, command.CodigoIbge, command.Nome,
            command.AutorizadorNFe, command.AutorizadorNFCe);

        await ufs.AddAsync(uf, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.uf.criada", "UfFiscal", sigla, ct);

        return ToDto(uf);
    }

    public async Task<UfFiscalDto?> UpdateAsync(UpdateUfCommand command, Guid userId, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(command, ct);

        var uf = await ufs.GetBySiglaAsync(command.Sigla, ct);
        if (uf is null) return null;

        var other = await ufs.GetByCodigoIbgeAsync(command.CodigoIbge, ct);
        if (other is not null && other.Id != uf.Id)
            throw new BusinessRuleViolationException($"cUF {command.CodigoIbge} já cadastrado em outra UF.");

        uf.Update(command.CodigoIbge, command.Nome, command.AutorizadorNFe, command.AutorizadorNFCe);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.uf.atualizada", "UfFiscal", uf.Sigla, ct);

        return ToDto(uf);
    }

    public async Task<bool> DeleteAsync(string sigla, Guid userId, CancellationToken ct = default)
    {
        var uf = await ufs.GetBySiglaAsync(sigla, ct);
        if (uf is null) return false;

        uf.SoftDelete();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.uf.removida", "UfFiscal", uf.Sigla, ct);
        return true;
    }

    public async Task<UfFiscalDto?> GetBySiglaAsync(string sigla, CancellationToken ct = default)
    {
        var uf = await ufs.GetBySiglaAsync(sigla, ct);
        return uf is null ? null : ToDto(uf);
    }

    public async Task<IReadOnlyList<UfFiscalDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await ufs.ListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<UfAmbientesDto?> GetAmbientesAsync(string sigla, CancellationToken ct = default)
    {
        var uf = await ufs.GetBySiglaAsync(sigla, ct);
        if (uf is null) return null;

        // Resolve autorizadores da UF para códigos de endpoint.
        var nfeAuths = AutorizadoresDe(uf.AutorizadorNFe, uf.Sigla);
        var nfceAuths = AutorizadoresDe(uf.AutorizadorNFCe, uf.Sigla);

        var nfeHom = new List<SefazEndpointDto>();
        var nfeProd = new List<SefazEndpointDto>();
        var nfceHom = new List<SefazEndpointDto>();
        var nfceProd = new List<SefazEndpointDto>();

        foreach (var auth in nfeAuths.Distinct())
        {
            var eps = await endpoints.ListByAutorizadorAsync(auth, ct);
            nfeHom.AddRange(eps.Where(e => e.Modelo == ModeloFiscal.NFe55 && e.Ambiente == AmbienteFiscal.Homologacao).Select(ToDto));
            nfeProd.AddRange(eps.Where(e => e.Modelo == ModeloFiscal.NFe55 && e.Ambiente == AmbienteFiscal.Producao).Select(ToDto));
        }

        foreach (var auth in nfceAuths.Distinct())
        {
            var eps = await endpoints.ListByAutorizadorAsync(auth, ct);
            nfceHom.AddRange(eps.Where(e => e.Modelo == ModeloFiscal.NFCe65 && e.Ambiente == AmbienteFiscal.Homologacao).Select(ToDto));
            nfceProd.AddRange(eps.Where(e => e.Modelo == ModeloFiscal.NFCe65 && e.Ambiente == AmbienteFiscal.Producao).Select(ToDto));
        }

        return new UfAmbientesDto(ToDto(uf), nfeHom, nfeProd, nfceHom, nfceProd);
    }

    private static List<string> AutorizadoresDe(AutorizadorTipo tipo, string sigla) => tipo switch
    {
        AutorizadorTipo.Proprio => [sigla],
        AutorizadorTipo.Svrs => ["SVRS"],
        AutorizadorTipo.Svan => ["SVAN"],
        _ => []
    };

    internal static UfFiscalDto ToDto(UfFiscal u) => new(
        u.Id.Value, u.Sigla, u.CodigoIbge, u.Nome,
        u.AutorizadorNFe, u.AutorizadorNFCe, u.IsActive);

    internal static SefazEndpointDto ToDto(SefazEndpoint e) => new(
        e.Id.Value, e.Autorizador, e.Modelo, e.Servico, e.Ambiente,
        e.VersaoServico, e.Url, e.VigenciaInicio, e.VigenciaFim,
        e.FonteUrl, e.VerificadoEm, e.Verificado, e.IsActive);
}
