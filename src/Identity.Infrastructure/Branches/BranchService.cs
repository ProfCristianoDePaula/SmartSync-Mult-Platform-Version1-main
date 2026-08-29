using FluentValidation;
using Identity.Application.Branches;
using Identity.Application.Common;
using Identity.Application.Plans;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Branches;

/// <summary>
/// Implementação do CRUD de filiais (Etapa 14). TODAS as operações recebem o
/// TenantId de forma explícita e filtram por ele — a query nunca atravessa
/// tenants (garantia contra vazamento de filiais de outro tenant). Valida com
/// FluentValidation, converte ArgumentException dos value objects em 400 e usa
/// o named query filter "Active" do Branch para soft delete (mesmo padrão de
/// Tenant/Plan). A autorização por claim (SuperAdmin global / TenantAdmin do
/// próprio tenant) fica na camada de API.
/// </summary>
public sealed class BranchService : IBranchService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IValidator<CreateBranchCommand> _createValidator;
    private readonly IValidator<UpdateBranchCommand> _updateValidator;
    private readonly IPlanLimitResolver _planLimitResolver;

    public BranchService(
        IdentityDbContext dbContext,
        IValidator<CreateBranchCommand> createValidator,
        IValidator<UpdateBranchCommand> updateValidator,
        IPlanLimitResolver planLimitResolver)
    {
        _dbContext = dbContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _planLimitResolver = planLimitResolver;
    }

    public async Task<BranchDto?> CreateAsync(CreateBranchCommand command, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(command, ct);

        var tenantId = TenantId.From(command.TenantId);

        // O tenant precisa existir e estar ATIVO (o query filter "Active" oculta
        // tenants soft-deletados). Sem isso, retorna null → 404 na API.
        var tenantExists = await _dbContext.Tenants
            .AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
            return null;

        // Limite de filiais do plano (pendência Etapas 13/14/15): só atinge o
        // limite contratado quando MaxBranches efetivo é um número (null = sem
        // limite). Regra de negócio violada → 400 na API.
        var limits = await _planLimitResolver.GetEffectiveAsync(command.TenantId, ct);
        if (limits.MaxBranches is int maxBranches)
        {
            var currentBranches = await _dbContext.Branches.CountAsync(b => b.TenantId == tenantId, ct);
            if (currentBranches >= maxBranches)
                throw new BusinessRuleViolationException(
                    $"Limite de filiais do plano atingido ({maxBranches}).",
                    "plan.limit.branches.exceeded");
        }

        var address = ParseAddress(command.Address);
        var contact = ParseContact(command.Contact);

        var branch = Branch.Create(tenantId, command.Name, address, contact);

        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(branch);
    }

    public async Task<BranchDto?> UpdateAsync(UpdateBranchCommand command, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(command, ct);

        var branch = await FindActiveAsync(command.TenantId, command.BranchId, ct);
        if (branch is null)
            return null;

        var address = ParseAddress(command.Address);
        var contact = ParseContact(command.Contact);

        branch.Update(command.Name, address, contact);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(branch);
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteBranchCommand command, CancellationToken ct = default)
    {
        var branch = await FindActiveAsync(command.TenantId, command.BranchId, ct);
        if (branch is null)
            return false;

        branch.SoftDelete();
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<BranchDto>> ListByTenantAsync(ListBranchesByTenantQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var tenantId = TenantId.From(query.TenantId);

        var source = query.IncludeInactive
            ? _dbContext.Branches.IgnoreQueryFilters(["Active"])
            : _dbContext.Branches;

        // O TenantId é sempre imposto: só filiais DESTE tenant entram na query.
        source = source.Where(b => b.TenantId == tenantId);

        var total = await source.CountAsync(ct);
        var branches = await source
            .OrderBy(b => b.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<BranchDto>(
            branches.Select(ToDto).ToList(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<BranchDto?> GetByIdAsync(GetBranchByIdQuery query, CancellationToken ct = default)
    {
        var branch = await FindActiveAsync(query.TenantId, query.BranchId, ct);
        return branch is null ? null : ToDto(branch);
    }

    /// <summary>Busca uma filial ATIVA escopada ao tenant (query filter "Active").
    /// Retorna null se a filial não existir, estiver inativa ou pertencer a outro
    /// tenant.</summary>
    private Task<Branch?> FindActiveAsync(Guid tenantId, Guid branchId, CancellationToken ct)
        => _dbContext.Branches
            .FirstOrDefaultAsync(
                b => b.TenantId == TenantId.From(tenantId) && b.Id == BranchId.From(branchId),
                ct);

    private static Address ParseAddress(BranchAddress input)
    {
        try
        {
            return new Address(
                input.Street,
                input.Number,
                input.Complement,
                input.District,
                input.City,
                input.State,
                input.PostalCode);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message, "branch.address.invalid");
        }
    }

    private static Contact ParseContact(BranchContact input)
    {
        try
        {
            var email = Email.Create(input.Email);
            return new Contact(input.Phone, email, input.SecondaryPhone);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message, "branch.contact.invalid");
        }
    }

    private static BranchDto ToDto(Branch branch) => new(
        branch.Id.Value,
        branch.TenantId.Value,
        branch.Name,
        new BranchAddress(
            branch.Address.Street,
            branch.Address.Number,
            branch.Address.Complement,
            branch.Address.District,
            branch.Address.City,
            branch.Address.State,
            branch.Address.PostalCode),
        new BranchContact(
            branch.Contact.Phone,
            branch.Contact.SecondaryPhone,
            branch.Contact.Email.Value),
        branch.IsActive,
        branch.DeletedAtUtc);
}
