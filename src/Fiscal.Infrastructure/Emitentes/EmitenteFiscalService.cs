using Fiscal.Application.Auditing;
using Fiscal.Application.Emitentes;
using Fiscal.Application.IntegrationServices;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Domain.ValueObjects;
using Fiscal.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Fiscal.Infrastructure.Emitentes;

public sealed class EmitenteFiscalService(
    IEmitenteFiscalRepository emitentes,
    IConfiguracaoDocumentoRepository configs,
    IConcessaoRepository concessoes,
    IFilialAccessChecker filiais,
    IUfFiscalRepository ufs,
    IMunicipioRepository municipios,
    ICertificadoReadModel certificados,
    FiscalDbContext db,
    IAuditLogger audit,
    IOptions<FiscalOptions> fiscalOptions,
    Sefaz.SefazAutorizador sefaz,
    IValidator<CreateEmitenteCommand> createValidator,
    IValidator<UpdateEmitenteCommand> updateValidator,
    IValidator<UpsertConfiguracaoCommand> configValidator) : IEmitenteFiscalService
{
    public const string ConfirmacaoProducao = "PROMOVER PARA PRODUCAO";

    public async Task<EmitenteDto> CreateAsync(CreateEmitenteCommand command, Guid userId, string role, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(command, ct);
        await ExigirEscritaAsync(command.TenantId, command.BranchId, userId, role, ct);

        var branchId = BranchId.From(command.BranchId);
        await ExigirFilialAsync(command.TenantId, branchId, ct);

        if (await emitentes.GetByBranchAsync(command.TenantId, branchId, ct) is not null)
            throw new BusinessRuleViolationException("Filial já possui emitente fiscal.");

        CnpjFiscal cnpj;
        try { cnpj = CnpjFiscal.Create(command.Cnpj); }
        catch (BusinessRuleViolationException) { throw; }
        catch (ArgumentException ex) { throw new BusinessRuleViolationException(ex.Message); }

        var endereco = new FiscalAddress(
            command.Endereco.Street, command.Endereco.Number, command.Endereco.Complement,
            command.Endereco.District, command.Endereco.City, command.Endereco.State,
            command.Endereco.PostalCode, command.Endereco.CodigoIbgeMunicipio);

        await ExigirUfMunicipioAsync(endereco, ct);

        var emitente = EmitenteFiscal.Create(command.TenantId, branchId, cnpj.Numero,
            command.RazaoSocial, command.Fantasia, command.InscricaoEstadual,
            command.InscricaoMunicipal, command.Cnae, command.Crt,
            endereco, command.Telefone, command.Email);

        await emitentes.AddAsync(emitente, ct);

        // Configurações padrão por tipo/ambiente: tudo Simulador, homologação habilitada.
        foreach (var tipo in new[] { TipoDocumentoFiscal.NFe55, TipoDocumentoFiscal.NFCe65, TipoDocumentoFiscal.NFSe })
        {
            await configs.AddAsync(ConfiguracaoDocumento.Create(emitente.Id, tipo,
                AmbienteFiscal.Homologacao, habilitado: true, serie: "1"), ct);
            await configs.AddAsync(ConfiguracaoDocumento.Create(emitente.Id, tipo,
                AmbienteFiscal.Producao, habilitado: false, serie: "1"), ct);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(command.TenantId, userId, "fiscal.emitente.criado", "EmitenteFiscal",
            emitente.Id.Value.ToString(), ct);

        return ToDto(emitente);
    }

    public async Task<EmitenteDto?> UpdateAsync(UpdateEmitenteCommand command, Guid userId, string role, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(command, ct);

        var emitente = await emitentes.GetAsync(command.TenantId, command.EmitenteId, ct);
        if (emitente is null) return null;

        await ExigirEscritaAsync(command.TenantId, emitente.BranchId.Value, userId, role, ct);

        var endereco = new FiscalAddress(
            command.Endereco.Street, command.Endereco.Number, command.Endereco.Complement,
            command.Endereco.District, command.Endereco.City, command.Endereco.State,
            command.Endereco.PostalCode, command.Endereco.CodigoIbgeMunicipio);

        await ExigirUfMunicipioAsync(endereco, ct);

        emitente.Update(command.RazaoSocial, command.Fantasia, command.InscricaoEstadual,
            command.InscricaoMunicipal, command.Cnae, command.Crt, endereco,
            command.Telefone, command.Email);

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(command.TenantId, userId, "fiscal.emitente.atualizado", "EmitenteFiscal",
            emitente.Id.Value.ToString(), ct);

        return ToDto(emitente);
    }

    public async Task<EmitenteDto?> GetByIdAsync(TenantId tenantId, Guid emitenteId, CancellationToken ct = default)
    {
        var emitente = await emitentes.GetAsync(tenantId, emitenteId, ct);
        return emitente is null ? null : ToDto(emitente);
    }

    public async Task<EmitenteDto?> GetByBranchAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default)
    {
        var emitente = await emitentes.GetByBranchAsync(tenantId, BranchId.From(branchId), ct);
        return emitente is null ? null : ToDto(emitente);
    }

    public async Task<EmitenteDto> DefinirEmissaoAutomaticaAsync(
        TenantId tenantId, Guid emitenteId, bool ativa, Guid userId, CancellationToken ct = default)
    {
        var emitente = await emitentes.GetAsync(tenantId, emitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");
        emitente.DefinirEmissaoAutomatica(ativa);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, userId, "fiscal.emitente.autoemissao", "EmitenteFiscal",
            $"{emitente.Id.Value}={ativa}", ct);
        return ToDto(emitente);
    }

    public async Task<IReadOnlyList<EmitenteDto>> ListAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var items = await emitentes.ListAsync(tenantId, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<ConfiguracaoDocumentoDto> UpsertConfiguracaoAsync(UpsertConfiguracaoCommand command, Guid userId, string role, CancellationToken ct = default)
    {
        await configValidator.ValidateAndThrowAsync(command, ct);

        var emitente = await emitentes.GetAsync(command.TenantId, command.EmitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");

        await ExigirEscritaAsync(command.TenantId, emitente.BranchId.Value, userId, role, ct);

        if (command.Ambiente == AmbienteFiscal.Producao)
            throw new BusinessRuleViolationException(
                "Produção só via promoção com gate (POST .../promover-producao).");

        var cfg = await configs.FindAsync(emitente.Id, command.Tipo, command.Ambiente, ct);
        if (cfg is null)
        {
            cfg = ConfiguracaoDocumento.Create(emitente.Id, command.Tipo, command.Ambiente,
                command.Habilitado, command.Serie, command.ModoIntegracao,
                command.ReferenciaCertificado, command.CscId);
            await configs.AddAsync(cfg, ct);
        }
        else
        {
            cfg.Update(command.Habilitado, command.Serie, command.ModoIntegracao,
                command.ReferenciaCertificado, command.CscId);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(command.TenantId, userId, "fiscal.config.atualizada", "ConfiguracaoDocumento",
            $"{emitente.Id.Value}/{(int)command.Tipo}/{(int)command.Ambiente}", ct);

        return ToDto(cfg);
    }

    public async Task<ProntidaoDto> GetProntidaoAsync(
        TenantId tenantId, Guid emitenteId, TipoDocumentoFiscal tipo, AmbienteFiscal ambiente, CancellationToken ct = default)
    {
        var emitente = await emitentes.GetAsync(tenantId, emitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");

        return await MontarProntidaoAsync(emitente, tipo, ambiente, ct);
    }

    public async Task<ProntidaoDto> PromoverProducaoAsync(PromoverProducaoCommand command, CancellationToken ct = default)
    {
        if (!string.Equals(command.TextoConfirmacao?.Trim(), ConfirmacaoProducao, StringComparison.Ordinal))
            throw new BusinessRuleViolationException(
                $"Digite exatamente \"{ConfirmacaoProducao}\" para confirmar.");

        if (!string.Equals(command.Role, PlatformRoles.TenantAdmin, StringComparison.Ordinal)
            && !string.Equals(command.Role, PlatformRoles.SuperAdmin, StringComparison.Ordinal))
            throw new BusinessRuleViolationException("Somente TenantAdmin promove para produção.");

        if (!fiscalOptions.Value.ProducaoHabilitada)
            throw new BusinessRuleViolationException(
                "Produção global desativada (Fiscal:ProducaoHabilitada=false).", "fiscal.producao-bloqueada");

        var emitente = await emitentes.GetAsync(command.TenantId, command.EmitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");

        var prontidao = await MontarProntidaoAsync(emitente, command.Tipo, AmbienteFiscal.Producao, ct);
        if (!prontidao.Pronto)
            throw new BusinessRuleViolationException(
                "Emitente sem prontidão para produção: " +
                string.Join("; ", prontidao.Itens.Where(i => !i.Ok).Select(i => i.Item)));

        var cfg = await configs.FindAsync(emitente.Id, command.Tipo, AmbienteFiscal.Producao, ct);
        if (cfg is null)
        {
            cfg = ConfiguracaoDocumento.Create(emitente.Id, command.Tipo, AmbienteFiscal.Producao,
                habilitado: true, serie: "1", ModoIntegracao.Producao);
            await configs.AddAsync(cfg, ct);
        }
        else
        {
            cfg.Update(habilitado: true, cfg.Serie, ModoIntegracao.Producao, cfg.ReferenciaCertificado, cfg.CscId);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(command.TenantId, command.UserId, "fiscal.producao.promovida", "EmitenteFiscal",
            $"{emitente.Id.Value}/{(int)command.Tipo}", ct);

        return await MontarProntidaoAsync(emitente, command.Tipo, AmbienteFiscal.Producao, ct);
    }

    public async Task<TesteConexaoDto> TestarConexaoAsync(
        TenantId tenantId, Guid emitenteId, Guid userId, string role, CancellationToken ct = default)
    {
        var emitente = await emitentes.GetAsync(tenantId, emitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");

        // Diagnóstico: TenantAdmin ou Manager/Seller com concessão (Emitir ou Configurar).
        var pode = string.Equals(role, PlatformRoles.TenantAdmin, StringComparison.Ordinal)
            || string.Equals(role, PlatformRoles.SuperAdmin, StringComparison.Ordinal);
        if (!pode && (string.Equals(role, PlatformRoles.Manager, StringComparison.Ordinal)
            || string.Equals(role, PlatformRoles.Seller, StringComparison.Ordinal)))
        {
            pode = await concessoes.FindAsync(tenantId, emitente.BranchId, userId, PapelUnidade.Emitir, ct) is not null
                || await concessoes.FindAsync(tenantId, emitente.BranchId, userId, PapelUnidade.Configurar, ct) is not null;
        }
        if (!pode)
            throw new UnauthorizedAccessException("Sem acesso a esta unidade.");

        Fiscal.Application.Emissao.TransmissaoResult resultado;
        try
        {
            resultado = await sefaz.StatusServicoAsync(tenantId, emitenteId, ct);
        }
        catch (TimeoutException)
        {
            return new TesteConexaoDto(false, null, "Timeout na SEFAZ.", null);
        }

        var online = resultado.Resultado == Fiscal.Application.Emissao.ResultadoTransmissao.Autorizado;
        DateTime? validadaEm = null;
        if (online)
        {
            var cfg = await configs.FindAsync(emitente.Id, TipoDocumentoFiscal.NFe55, AmbienteFiscal.Homologacao, ct);
            cfg?.MarcarHomologacaoValidada();
            await db.SaveChangesAsync(ct);
            validadaEm = cfg?.HomologacaoValidadaEm;
            await audit.LogAsync(tenantId, userId, "fiscal.conexao.testada", "EmitenteFiscal",
                emitente.Id.Value.ToString(), ct);
        }

        return new TesteConexaoDto(online, resultado.CStat, resultado.Motivo, validadaEm);
    }

    private async Task<ProntidaoDto> MontarProntidaoAsync(
        EmitenteFiscal emitente, TipoDocumentoFiscal tipo, AmbienteFiscal ambiente, CancellationToken ct)
    {
        var itens = new List<ProntidaoItemDto>
        {
            new("dados-cadastrais", true, "CNPJ, razão, endereço com IBGE e contato completos."),
        };

        var uf = await ufs.GetBySiglaAsync(emitente.Endereco.State, ct);
        itens.Add(uf is null
            ? new("uf-valida", false, $"UF {emitente.Endereco.State} fora do catálogo.")
            : new("uf-valida", true, $"UF {uf.Sigla} ({uf.Nome})."));

        var mun = await municipios.GetByIbgeAsync(emitente.Endereco.CodigoIbgeMunicipio, ct);
        itens.Add(mun is null
            ? new("municipio-valido", false, $"IBGE {emitente.Endereco.CodigoIbgeMunicipio} fora do catálogo.")
            : new("municipio-valido", true, $"{mun.Nome}/{mun.Uf}."));

        var cfg = await configs.FindAsync(emitente.Id, tipo, ambiente, ct);
        itens.Add(cfg is null
            ? new("serie-definida", false, "Configuração do tipo/ambiente inexistente.")
            : new("serie-definida", true, $"Série {cfg.Serie}, modo {cfg.ModoIntegracao}."));

        if (tipo == TipoDocumentoFiscal.NFCe65)
            itens.Add(string.IsNullOrWhiteSpace(cfg?.CscId)
                ? new("csc-nfce", false, "CSC (id) obrigatório para NFC-e.")
                : new("csc-nfce", true, "CSC id cadastrado (token chega com o cofre)."));

        var cert = cfg?.ReferenciaCertificado is not null
            ? await certificados.GetAsync(emitente.TenantId, cfg.ReferenciaCertificado.Value, ct)
            : null;
        itens.Add(cert is null
            ? new("certificado-valido", false, "Nenhum certificado válido vinculado (Fiscal-5).")
            : cert.Expirado
                ? new("certificado-valido", false, $"Certificado expirado em {cert.NotAfter:yyyy-MM-dd}.")
                : new("certificado-valido", true, $"Certificado válido até {cert.NotAfter:yyyy-MM-dd}."));

        var cfgHom = await configs.FindAsync(emitente.Id, tipo, AmbienteFiscal.Homologacao, ct);
        itens.Add(cfgHom?.HomologacaoValidadaEm is not null
            ? new("conexao-homologacao", true, $"StatusServico OK em {cfgHom.HomologacaoValidadaEm:yyyy-MM-dd HH:mm}Z.")
            : new("conexao-homologacao", false, "Execute POST .../testar-conexao (Fiscal-10)."));

        return new ProntidaoDto(emitente.Id.Value, tipo, ambiente,
            itens.All(i => i.Ok), itens);
    }

    private async Task ExigirUfMunicipioAsync(FiscalAddress endereco, CancellationToken ct)
    {
        if (await ufs.GetBySiglaAsync(endereco.State, ct) is null)
            throw new BusinessRuleViolationException($"UF {endereco.State} fora do catálogo fiscal.");
        if (await municipios.GetByIbgeAsync(endereco.CodigoIbgeMunicipio, ct) is null)
            throw new BusinessRuleViolationException(
                $"Município IBGE {endereco.CodigoIbgeMunicipio} fora do catálogo fiscal.");
    }

    private async Task ExigirFilialAsync(TenantId tenantId, BranchId branchId, CancellationToken ct)
    {
        var acesso = await filiais.ValidateAsync(tenantId, branchId.Value, ct);
        if (acesso != FilialAccess.Allowed)
            throw new BusinessRuleViolationException(
                "Filial não pertence a este tenant (ou não pôde ser validada).");
    }

    private async Task ExigirEscritaAsync(
        TenantId tenantId, Guid branchId, Guid userId, string role, CancellationToken ct)
    {
        if (string.Equals(role, PlatformRoles.TenantAdmin, StringComparison.Ordinal)
            || string.Equals(role, PlatformRoles.SuperAdmin, StringComparison.Ordinal))
            return;

        if (string.Equals(role, PlatformRoles.Manager, StringComparison.Ordinal)
            || string.Equals(role, PlatformRoles.Seller, StringComparison.Ordinal))
        {
            if (await concessoes.FindAsync(tenantId, BranchId.From(branchId), userId, PapelUnidade.Configurar, ct) is not null)
                return;
        }

        throw new UnauthorizedAccessException(
            "Sem concessão de configuração nesta unidade.");
    }

    internal static EmitenteDto ToDto(EmitenteFiscal e) => new(
        e.Id.Value, e.BranchId.Value, e.Cnpj.Numero, e.Cnpj.Alfanumerico,
        e.RazaoSocial, e.Fantasia, e.InscricaoEstadual, e.InscricaoMunicipal,
        e.Cnae, e.Crt,
        new EnderecoFiscalDto(e.Endereco.Street, e.Endereco.Number, e.Endereco.Complement,
            e.Endereco.District, e.Endereco.City, e.Endereco.State,
            e.Endereco.PostalCode, e.Endereco.CodigoIbgeMunicipio),
        e.Telefone, e.Email, e.EmissaoAutomaticaVenda, e.IsActive);

    internal static ConfiguracaoDocumentoDto ToDto(ConfiguracaoDocumento c) => new(
        c.Id.Value, c.EmitenteId.Value, c.Tipo, c.Ambiente, c.Habilitado,
        c.Serie, c.ModoIntegracao, c.ReferenciaCertificado, c.CscId);
}
