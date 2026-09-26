using Fiscal.Application.Auditing;
using Fiscal.Application.Cadastros;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Domain.ValueObjects;
using Fiscal.Infrastructure.Persistence;
using FluentValidation;

namespace Fiscal.Infrastructure.Cadastros;

public sealed class ClienteFiscalService(
    IClienteFiscalRepository clientes,
    FiscalDbContext db,
    IValidator<UpsertClienteFiscalCommand> validator) : IClienteFiscalService
{
    public async Task<ClienteFiscalDto> UpsertAsync(UpsertClienteFiscalCommand command, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var endereco = new FiscalAddress(
            command.Street, command.Number, command.Complement, command.District,
            command.City, command.State, command.PostalCode, command.CodigoIbgeMunicipio);

        var atual = await clientes.GetAsync(command.TenantId, command.ClienteId, ct);
        if (atual is null)
        {
            atual = ClienteFiscal.Create(command.TenantId, command.ClienteId,
                command.TipoPessoa, command.Documento, command.Nome, command.IndicadorIe,
                command.InscricaoEstadual, endereco, command.Email, command.ConsumidorFinal);
            await clientes.AddAsync(atual, ct);
        }
        else
        {
            atual.Update(command.TipoPessoa, command.Documento, command.Nome,
                command.IndicadorIe, command.InscricaoEstadual, endereco,
                command.Email, command.ConsumidorFinal);
        }

        await db.SaveChangesAsync(ct);
        return ToDto(atual);
    }

    public async Task<ClienteFiscalDto?> GetAsync(TenantId tenantId, Guid clienteId, CancellationToken ct = default)
    {
        var atual = await clientes.GetAsync(tenantId, clienteId, ct);
        return atual is null ? null : ToDto(atual);
    }

    public async Task<IReadOnlyList<string>> ValidarAsync(TenantId tenantId, Guid clienteId, CancellationToken ct = default)
    {
        var atual = await clientes.GetAsync(tenantId, clienteId, ct);
        if (atual is null) return ["Sem perfil fiscal cadastrado."];

        var pendencias = new List<string>();
        if (atual.IndicadorIe == IndicadorIe.Contribuinte && string.IsNullOrWhiteSpace(atual.InscricaoEstadual))
            pendencias.Add("IE ausente para contribuinte.");
        return pendencias;
    }

    private static ClienteFiscalDto ToDto(ClienteFiscal c) => new(
        c.ClienteId, c.TipoPessoa, c.Documento, c.Nome, c.IndicadorIe,
        c.InscricaoEstadual, c.ConsumidorFinal);
}

public sealed class NaturezaService(
    INaturezaRepository naturezas,
    FiscalDbContext db,
    IAuditLogger audit) : INaturezaService
{
    public async Task<NaturezaOperacaoDto> CreateAsync(CreateNaturezaCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Codigo) || string.IsNullOrWhiteSpace(command.Descricao))
            throw new BusinessRuleViolationException("Código e descrição obrigatórios.");

        if (await naturezas.GetByCodigoAsync(command.TenantId, command.Codigo, ct) is not null)
            throw new BusinessRuleViolationException($"Natureza '{command.Codigo.Trim().ToUpperInvariant()}' já cadastrada.");

        var natureza = NaturezaOperacao.Create(command.TenantId, command.Codigo, command.Descricao, command.TipoOperacao);
        await naturezas.AddAsync(natureza, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(command.TenantId, null, "fiscal.natureza.criada", "NaturezaOperacao", natureza.Codigo, ct);

        return new NaturezaOperacaoDto(natureza.Id.Value, natureza.Codigo, natureza.Descricao, natureza.TipoOperacao);
    }

    public async Task<IReadOnlyList<NaturezaOperacaoDto>> ListAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var items = await naturezas.ListAsync(tenantId, ct);
        return items.Select(n => new NaturezaOperacaoDto(n.Id.Value, n.Codigo, n.Descricao, n.TipoOperacao)).ToList();
    }

    public async Task<bool> DeleteAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
    {
        var natureza = await naturezas.GetAsync(tenantId, id, ct);
        if (natureza is null) return false;
        natureza.SoftDelete();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
