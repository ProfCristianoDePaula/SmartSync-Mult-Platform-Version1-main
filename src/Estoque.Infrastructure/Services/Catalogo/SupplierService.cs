using Estoque.Application.Catalogo;
using Estoque.Application.Common;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.ValueObjects;

namespace Estoque.Infrastructure.Services.Catalogo;

public sealed class SupplierService(ISupplierRepository suppliers, IUnitOfWork uow) : ISupplierService
{
    public async Task<SupplierDto?> CreateAsync(CreateSupplierCommand command, CancellationToken ct = default)
    {
        var document = NormalizeDocument(command.TipoPessoa, command.Documento);
        var existing = await suppliers.GetByDocumentAsync(command.TenantId, document, ct);
        if (existing is not null)
            throw new BusinessRuleViolationException(
                "Já existe um fornecedor com este documento (CPF/CNPJ) neste tenant.");

        var supplier = Supplier.Create(
            command.TenantId, command.Name, command.TipoPessoa, document, command.Email,
            ToAddress(command.Address), ToContact(command.Contact));

        await suppliers.AddAsync(supplier, ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(supplier);
    }

    public async Task<SupplierDto?> UpdateAsync(UpdateSupplierCommand command, CancellationToken ct = default)
    {
        var supplier = await suppliers.GetAsync(command.TenantId, command.SupplierId, ct);
        if (supplier is null || !supplier.IsActive)
            return null;

        // Documento é IMUTÁVEL no update (padrão do Tenant do Identity).
        supplier.Update(command.Name, command.Email,
                        ToAddress(command.Address), ToContact(command.Contact));
        await uow.SaveChangesAsync(ct);
        return ToDto(supplier);
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteSupplierCommand command, CancellationToken ct = default)
    {
        var supplier = await suppliers.GetAsync(command.TenantId, command.SupplierId, ct);
        if (supplier is null || !supplier.IsActive) return false;
        supplier.SoftDelete();
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<SupplierDto>> ListAsync(ListSuppliersQuery query, CancellationToken ct = default)
    {
        var (items, total) = await suppliers.ListAsync(query.TenantId, query.Search, query.Page, query.PageSize, ct);
        return new PagedResult<SupplierDto>(
            items.Select(ToDto).ToList(), query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    private static string NormalizeDocument(int tipoPessoa, string document)
        => tipoPessoa == 1
            ? Cpf.Create(document).Number
            : Cnpj.Create(document).Number;

    private static Address ToAddress(AddressInput? input)
        => input is null
            ? throw new BusinessRuleViolationException("Endereço é obrigatório.")
            : new Address(input.Street, input.Number, input.Complement,
                          input.District, input.City, input.State, input.PostalCode);

    private static Contact ToContact(ContactInput? input)
        => input is null
            ? throw new BusinessRuleViolationException("Contato é obrigatório.")
            : new Contact(input.Phone, Email.Create(string.IsNullOrWhiteSpace(input.Email)
                    ? throw new BusinessRuleViolationException("E-mail de contato é obrigatório.")
                    : input.Email),
                input.SecondaryPhone);

    private static SupplierDto ToDto(Supplier s) => new(
        s.Id.Value, s.Name, (int)s.TipoPessoa,
        s.TipoPessoa == Domain.Enums.TipoPessoa.Fisica ? Cpf.FromValidated(s.DocumentNumber).ToString() : Cnpj.FromValidated(s.DocumentNumber).ToString(),
        s.Email.Value, s.IsActive);
}
