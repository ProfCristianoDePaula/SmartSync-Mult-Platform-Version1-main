using FluentValidation;
using Identity.Application.Common;
using Identity.Application.Tenants;
using Identity.Application.Uniqueness;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Tenants;

/// <summary>
/// Implementação do CRUD de tenants. Valida com FluentValidation (Application),
/// reutiliza as regras GLOBAIS de unicidade de documento/e-mail (Etapa 04) e
/// aplica soft delete via named query filter ("Active"): tenants inativados
/// ficam invisíveis nas consultas e seus usuários não podem mais autenticar
/// (verificação no AuthService). O soft delete também REVOGA os refresh tokens
/// ativos de todos os usuários do tenant (pendência da Etapa 13). Módulos/planos
/// do tenant são gerenciados pelo TenantModuleService (Etapa 15), não aqui.
/// </summary>
public sealed class TenantService : ITenantService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IValidator<CreateTenantCommand> _createValidator;
    private readonly IValidator<UpdateTenantCommand> _updateValidator;
    private readonly TenantUniquenessValidator _uniqueness;
    private readonly TokenService _tokenService;

    public TenantService(
        IdentityDbContext dbContext,
        IValidator<CreateTenantCommand> createValidator,
        IValidator<UpdateTenantCommand> updateValidator,
        TenantUniquenessValidator uniqueness,
        TokenService tokenService)
    {
        _dbContext = dbContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _uniqueness = uniqueness;
        _tokenService = tokenService;
    }

    public async Task<TenantDto> CreateAsync(CreateTenantCommand command, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(command, ct);

        var documento = ParseDocumento(command.TipoPessoa, command.Documento);
        var email = ParseEmail(command.Email);

        await _uniqueness.ValidateAsync(documento, email, excludeTenantId: null, ct);

        var tenant = Tenant.Create(command.LegalName, command.TradeName, documento, email);

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(tenant);
    }

    public async Task<TenantDto?> UpdateAsync(UpdateTenantCommand command, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(command, ct);

        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == TenantId.From(command.Id), ct);
        if (tenant is null)
            return null;

        var email = ParseEmail(command.Email);

        // O documento (CPF/CNPJ) é imutável (identidade); apenas o e-mail é
        // checado no update, mas o documento não pode colidir com outro tenant ativo.
        await _uniqueness.EnsureDocumentoUniqueAsync(tenant.Documento, tenant.Id, ct);
        await _uniqueness.EnsureEmailUniqueAsync(email, tenant.Id, ct);

        tenant.Update(
            command.LegalName,
            command.TradeName,
            email,
            command.Status);

        await _dbContext.SaveChangesAsync(ct);

        return ToDto(tenant);
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteTenantCommand command, CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == TenantId.From(command.Id), ct);
        if (tenant is null)
            return false;

        // Transação: soft delete + revogação em massa dos refresh tokens dos
        // usuários do tenant (pendência da Etapa 13) são atômicos.
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        tenant.SoftDelete();
        await _dbContext.SaveChangesAsync(ct);

        await _tokenService.RevokeAllTenantRefreshTokensAsync(command.Id, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<PagedResult<TenantDto>> ListAsync(ListTenantsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // includeInactive = true quebra o query filter "Active" e lista também
        // os tenants soft-deletados.
        var source = query.IncludeInactive
            ? _dbContext.Tenants.IgnoreQueryFilters(["Active"])
            : _dbContext.Tenants;

        if (query.Status is not null)
            source = source.Where(t => t.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            source = source.Where(t =>
                EF.Functions.ILike(t.LegalName, pattern) ||
                EF.Functions.ILike(t.TradeName, pattern));
        }

        // Filtro por documento (CPF 11 / CNPJ 14 dígitos) na coluna do número —
        // OwnsOne não permite comparar o VO inteiro em query, por isso usamos o
        // número normalizado (Etapa 17).
        if (!string.IsNullOrWhiteSpace(query.Documento))
        {
            var digits = new string(query.Documento.Where(char.IsDigit).ToArray());
            if (digits.Length is 11 or 14)
                source = source.Where(t => t.Documento.Numero == digits);
        }

        var total = await source.CountAsync(ct);
        var tenants = await source
            .OrderBy(t => t.TradeName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TenantDto>(
            tenants.Select(ToDto).ToList(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<TenantDto?> GetByIdAsync(GetTenantByIdQuery query, CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == TenantId.From(query.Id), ct);
        return tenant is null ? null : ToDto(tenant);
    }

    private static Documento ParseDocumento(TipoPessoa tipo, string raw)
    {
        try
        {
            return Documento.Create(tipo, raw);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message, "tenant.document.invalid");
        }
    }

    private static Email ParseEmail(string raw)
    {
        try
        {
            return Email.Create(raw);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message, "tenant.email.invalid");
        }
    }

    private static TenantDto ToDto(Tenant tenant) => new(
        tenant.Id.Value,
        tenant.LegalName,
        tenant.TradeName,
        tenant.Documento.Tipo,
        tenant.Documento.Numero,
        tenant.Email.Value,
        tenant.Status,
        tenant.IsActive,
        tenant.CreatedAtUtc,
        tenant.DeletedAtUtc);
}
