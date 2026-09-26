using Fiscal.Application.Auditing;
using Fiscal.Application.Catalogo;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Fiscal.Infrastructure.Catalogo;

public sealed class SefazEndpointService(
    ISefazEndpointRepository endpoints,
    FiscalDbContext db,
    IAuditLogger audit,
    IOptions<SefazCatalogOptions> options,
    IValidator<CreateEndpointCommand> createValidator,
    IValidator<UpdateEndpointCommand> updateValidator) : ISefazEndpointService
{
    public async Task<SefazEndpointDto> CreateAsync(CreateEndpointCommand command, Guid userId, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(command, ct);
        AssertHostPermitido(command.Url);

        var autorizador = command.Autorizador.Trim().ToUpperInvariant();
        var versao = command.VersaoServico.Trim();

        if (await endpoints.FindActiveAsync(autorizador, command.Modelo, command.Servico, command.Ambiente, versao, ct) is not null)
            throw new BusinessRuleViolationException(
                $"Já existe endpoint ativo para {autorizador} modelo {(int)command.Modelo} serviço {command.Servico} ambiente {command.Ambiente} versão {versao}.");

        var endpoint = SefazEndpoint.Create(autorizador, command.Modelo, command.Servico,
            command.Ambiente, versao, command.Url.Trim(), command.VigenciaInicio, command.VigenciaFim,
            command.FonteUrl, command.VerificadoEm, command.Verificado);

        await endpoints.AddAsync(endpoint, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.endpoint.criado", "SefazEndpoint", endpoint.Id.Value.ToString(), ct);

        return UfFiscalService.ToDto(endpoint);
    }

    public async Task<SefazEndpointDto?> UpdateAsync(UpdateEndpointCommand command, Guid userId, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(command, ct);
        AssertHostPermitido(command.Url);

        var endpoint = await endpoints.GetAsync(command.Id, ct);
        if (endpoint is null) return null;

        endpoint.Update(command.VersaoServico.Trim(), command.Url.Trim(), command.VigenciaInicio,
            command.VigenciaFim, command.FonteUrl, command.VerificadoEm, command.Verificado);

        // A chave natural pode ter mudado (versão): garante unicidade entre ativos.
        var conflito = await endpoints.FindActiveAsync(endpoint.Autorizador, endpoint.Modelo,
            endpoint.Servico, endpoint.Ambiente, endpoint.VersaoServico, ct);
        if (conflito is not null && conflito.Id != endpoint.Id)
            throw new BusinessRuleViolationException("A nova versão conflita com outro endpoint ativo.");

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.endpoint.atualizado", "SefazEndpoint", endpoint.Id.Value.ToString(), ct);

        return UfFiscalService.ToDto(endpoint);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var endpoint = await endpoints.GetAsync(id, ct);
        if (endpoint is null) return false;

        endpoint.SoftDelete();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.endpoint.removido", "SefazEndpoint", id.ToString(), ct);
        return true;
    }

    public async Task<SefazEndpointDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await endpoints.GetAsync(id, ct);
        return endpoint is null ? null : UfFiscalService.ToDto(endpoint);
    }

    public async Task<IReadOnlyList<SefazEndpointDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await endpoints.ListAllAsync(ct);
        return items.Select(UfFiscalService.ToDto).ToList();
    }

    public async Task<SefazImportReport> ImportAsync(SefazImportRequest request, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Versao))
            throw new BusinessRuleViolationException("Versão do pacote de importação é obrigatória.");
        if (request.Endpoints is null || request.Endpoints.Count == 0)
            throw new BusinessRuleViolationException("Pacote de importação vazio.");

        int criados = 0, atualizados = 0, inalterados = 0;
        var erros = new List<SefazImportError>();

        for (var i = 0; i < request.Endpoints.Count; i++)
        {
            var linha = i + 1;
            var row = request.Endpoints[i];

            var erro = ValidarLinha(row);
            if (erro is not null)
            {
                erros.Add(new SefazImportError(linha, erro));
                continue;
            }

            var autorizador = row.Autorizador.Trim().ToUpperInvariant();
            var versao = row.VersaoServico.Trim();
            var modelo = (ModeloFiscal)row.Modelo;
            var servico = (SefazServico)row.Servico;
            var ambiente = (AmbienteFiscal)row.Ambiente;

            var existente = await endpoints.FindActiveAsync(autorizador, modelo, servico, ambiente, versao, ct);
            if (existente is null)
            {
                await endpoints.AddAsync(SefazEndpoint.Create(autorizador, modelo, servico, ambiente,
                    versao, row.Url.Trim(), row.VigenciaInicio, row.VigenciaFim, row.FonteUrl,
                    verificadoEm: DateTime.UtcNow, verificado: row.FonteUrl is not null), ct);
                criados++;
            }
            else if (EndpointIgual(existente, row))
            {
                inalterados++;
            }
            else
            {
                existente.Update(versao, row.Url.Trim(), row.VigenciaInicio, row.VigenciaFim,
                    row.FonteUrl, DateTime.UtcNow, row.FonteUrl is not null);
                atualizados++;
            }
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(null, userId, "fiscal.endpoints.importados", "SefazEndpoint",
            $"v{request.Versao}: +{criados} ~{atualizados} ={inalterados} x{erros.Count}", ct);

        return new SefazImportReport(request.Endpoints.Count, criados, atualizados, inalterados, erros.Count, erros);
    }

    private string? ValidarLinha(SefazImportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Autorizador) || row.Autorizador.Trim().Length > 20)
            return "Autorizador obrigatório (≤20).";
        if (row.Modelo is not (55 or 65))
            return "Modelo deve ser 55 ou 65.";
        if (!Enum.IsDefined(typeof(SefazServico), row.Servico))
            return "Serviço inválido.";
        if (!Enum.IsDefined(typeof(AmbienteFiscal), row.Ambiente))
            return "Ambiente inválido (1=Homologacao, 2=Producao).";
        if (string.IsNullOrWhiteSpace(row.VersaoServico) || row.VersaoServico.Trim().Length > 20)
            return "VersaoServico obrigatória (≤20).";
        if (string.IsNullOrWhiteSpace(row.Url))
            return "URL obrigatória.";
        try { AssertHostPermitido(row.Url); }
        catch (BusinessRuleViolationException ex) { return ex.Message; }
        return null;
    }

    private static bool EndpointIgual(SefazEndpoint e, SefazImportRow row)
        => e.Url == row.Url.Trim()
           && (e.FonteUrl ?? "") == (row.FonteUrl?.Trim() ?? "")
           && e.VigenciaInicio == row.VigenciaInicio
           && e.VigenciaFim == row.VigenciaFim;

    /// <summary>Host precisa ser HTTPS e terminar com sufixo permitido (default: .gov.br).</summary>
    private void AssertHostPermitido(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new BusinessRuleViolationException("URL deve ser HTTPS absoluta.");

        var host = uri.Host.ToLowerInvariant();
        var permitidos = options.Value.AllowedHostSuffixes
            .Select(s => s.ToLowerInvariant())
            .ToList();

        if (!permitidos.Any(s => host.EndsWith(s, StringComparison.Ordinal)))
            throw new BusinessRuleViolationException(
                $"Host '{uri.Host}' não permitido. Sufixos aceitos: {string.Join(", ", permitidos)}.");
    }
}
