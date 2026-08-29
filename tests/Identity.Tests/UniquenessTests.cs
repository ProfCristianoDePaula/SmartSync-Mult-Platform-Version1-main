using Identity.Application.Uniqueness;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Identity.Tests.Lab;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Unicidade CPF/CNPJ/e-mail: documento (CPF/CNPJ) e e-mail de tenant são
/// GLOBAIS; e-mail e documento de usuário são POR TENANT (o mesmo valor pode
/// existir em tenants diferentes). Valida-se a camada de regra (validators) e a
/// constraint do banco.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class UniquenessTests
{
    private readonly IdentityApiFactory _factory;

    public UniquenessTests(IdentityApiFactory factory) => _factory = factory;

    // ==================== Tenant (escopo global) ====================

    [Fact]
    public async Task Tenant_CnpjDuplicado_LancaViolacaoDeRegra()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);

        var validator = sp.GetRequiredService<TenantUniquenessValidator>();
        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            validator.EnsureDocumentoUniqueAsync(tenant.Documento));

        Assert.Equal("tenant.document.duplicate", ex.Code);
    }

    [Fact]
    public async Task Tenant_CpfDuplicado_LancaViolacaoDeRegra()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IdentityDbContext>();
        var cpf = TestData.UniqueCpf();
        db.Tenants.Add(Tenant.Create(
            $"Razao Social {Guid.NewGuid():N}",
            $"Nome Fantasia {Guid.NewGuid():N}",
            Documento.Create(TipoPessoa.Fisica, cpf),
            Email.Create($"tenant-{Guid.NewGuid():N}@teste.local")));
        await db.SaveChangesAsync();

        var validator = sp.GetRequiredService<TenantUniquenessValidator>();
        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            validator.EnsureDocumentoUniqueAsync(Documento.Create(TipoPessoa.Fisica, cpf)));

        Assert.Equal("tenant.document.duplicate", ex.Code);
    }

    [Fact]
    public async Task Tenant_EmailDuplicado_LancaViolacaoDeRegra()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);

        var validator = sp.GetRequiredService<TenantUniquenessValidator>();
        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            validator.EnsureEmailUniqueAsync(tenant.Email));

        Assert.Equal("tenant.email.duplicate", ex.Code);
    }

    [Fact]
    public async Task Tenant_DocumentoDiferente_PassaValidacao()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await TestData.CreateTenantAsync(sp);

        var validator = sp.GetRequiredService<TenantUniquenessValidator>();
        // Documento e e-mail inéditos não devem lançar.
        await validator.ValidateAsync(
            Documento.Create(TipoPessoa.Juridica, TestData.UniqueCnpj()),
            Email.Create($"novo-{Guid.NewGuid():N}@teste.local"));
    }

    // ==================== Usuário (escopo por tenant) ====================

    [Fact]
    public async Task User_MesmoEmailMesmoTenant_LancaViolacao()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        var validator = sp.GetRequiredService<UserUniquenessValidator>();
        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            validator.EnsureEmailUniqueAsync(tenant.Id, user.Email!));

        Assert.Equal("user.email.duplicate", ex.Code);
    }

    [Fact]
    public async Task User_MesmoEmailTenantsDiferentes_NoException()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);

        var userA = await TestData.CreateUserAsync(sp, tenantA.Id, Roles.Client);
        var email = userA.Email!;

        // Mesmo e-mail em outro tenant é permitido.
        var validator = sp.GetRequiredService<UserUniquenessValidator>();
        await validator.EnsureEmailUniqueAsync(tenantB.Id, email);
    }

    [Fact]
    public async Task User_MesmoDocumentoMesmoTenant_LancaViolacao()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);

        var cpf = TestData.UniqueCnpj();
        await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client, document: cpf);

        var validator = sp.GetRequiredService<UserUniquenessValidator>();
        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            validator.EnsureDocumentUniqueAsync(tenant.Id, cpf));

        Assert.Equal("user.document.duplicate", ex.Code);
    }

    [Fact]
    public async Task User_MesmoDocumentoTenantsDiferentes_NoException()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);

        var cpf = TestData.UniqueCnpj();
        await TestData.CreateUserAsync(sp, tenantA.Id, Roles.Client, document: cpf);

        var validator = sp.GetRequiredService<UserUniquenessValidator>();
        await validator.EnsureDocumentUniqueAsync(tenantB.Id, cpf);
    }

    // ==================== Constraints no banco (defesa em profundidade) ====================

    [Fact]
    public async Task Banco_UsuarioMesmoEmailMesmoTenant_FalhaAoSalvar()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);

        var primeiro = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        // Duplicata criada "na mão" (sem passar pelo validator): mesmo e-mail no
        // mesmo tenant. A constraint única IX_users_tenant_email deve barrar.
        var db = sp.GetRequiredService<IdentityDbContext>();
        var dup = new Identity.Infrastructure.Persistence.Identity.ApplicationUser
        {
            UserName = TestData.UniqueEmail(),
            Email = primeiro.Email,
            FullName = "Duplicata",
            TenantId = tenant.Id,
            EmailConfirmed = false
        };

        db.Users.Add(dup);
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => db.SaveChangesAsync());
    }
}