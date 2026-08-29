using System.Net.Http.Json;
using Identity.Application.Auth;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Tests.Lab;

/// <summary>
/// Fábricas de dados de teste: tenant, usuário, roles e requests de login.
/// Todos os valores gerados são únicos por chamada (Guid), garantindo
/// isolamento entre testes na suíte de integração.
/// </summary>
public static class TestData
{
    public static string UniqueEmail(string prefix = "user")
        => $"{prefix}-{Guid.NewGuid():N}@teste.local";

    /// <summary>CNPJ válido e único (base aleatória + dígitos verificadores corretos).</summary>
    public static string UniqueCnpj()
    {
        while (true)
        {
            var baseDigits = RandomDigits(8) + "0001";
            var cnpj = baseDigits + ComputeCnpjCheckDigits(baseDigits);
            try
            {
                Cnpj.Create(cnpj);
                return cnpj;
            }
            catch (ArgumentException)
            {
                // tenta outra base
            }
        }
    }

    /// <summary>CPF válido e único (base aleatória + dígitos verificadores corretos).</summary>
    public static string UniqueCpf()
    {
        while (true)
        {
            var baseDigits = RandomDigits(9);
            var cpf = baseDigits + ComputeCpfCheckDigits(baseDigits);
            try
            {
                Cpf.Create(cpf);
                return cpf;
            }
            catch (ArgumentException)
            {
                // tenta outra base
            }
        }
    }

    public static string UniquePassword() => "Senha!2026x";

    public static LoginRequest Login(string identifier, string password)
        => new(identifier, password);

    /// <summary>Cria um tenant (pessoa jurídica por padrão) e persiste (único por chamada).</summary>
    public static async Task<Tenant> CreateTenantAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var tenant = Tenant.Create(
            $"Razao Social {Guid.NewGuid():N}",
            $"Nome Fantasia {Guid.NewGuid():N}",
            Documento.Create(TipoPessoa.Juridica, UniqueCnpj()),
            Email.Create($"tenant-{Guid.NewGuid():N}@teste.local"));

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    /// <summary>Cria um módulo e persiste (único por chamada).</summary>
    public static async Task<Module> CreateModuleAsync(IServiceProvider services, string? name = null, string? slug = null)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var module = Module.Create(
            name ?? $"Modulo {Guid.NewGuid():N}",
            slug ?? $"modulo{Guid.NewGuid():N}",
            null);

        db.Modules.Add(module);
        await db.SaveChangesAsync();
        return module;
    }

    /// <summary>Cria um plano de assinatura de um módulo e persiste (único por chamada).</summary>
    public static async Task<Plan> CreatePlanAsync(IServiceProvider services, ModuleId moduleId, string? name = null)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var plan = Plan.Create(
            moduleId,
            name ?? $"Plano {Guid.NewGuid():N}",
            null,
            49.90m,
            499.00m,
            7,
            ["Dashboard"],
            5,
            10,
            1024);

        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    /// <summary>
    /// Cria um usuário no tenant informado (ou global se <paramref name="tenantId"/>
    /// for nulo), com a role e o estado de confirmação de e-mail desejados.
    /// </summary>
    public static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        TenantId? tenantId,
        string role,
        bool emailConfirmed = true,
        string? email = null,
        string? document = null)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new ApplicationRole(role));

        email ??= UniqueEmail();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = $"Teste {Guid.NewGuid():N}",
            TenantId = tenantId,
            EmailConfirmed = emailConfirmed,
            Document = document
        };

        var create = await userManager.CreateAsync(user, UniquePassword());
        if (!create.Succeeded)
            throw new InvalidOperationException(
                "Falha ao criar usuário de teste: " + string.Join("; ", create.Errors.Select(e => e.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
            throw new InvalidOperationException("Falha ao atribuir role ao usuário de teste.");

        return user;
    }

    /// <summary>Loga e retorna o par de tokens (access + refresh).</summary>
    public static async Task<TokenResponse> LoginAsync(
        IdentityApiFactory factory,
        string identifier,
        string password)
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", Login(identifier, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static string RandomDigits(int count)
    {
        var random = new Random(Guid.NewGuid().GetHashCode());
        var sb = new System.Text.StringBuilder(count);
        for (var i = 0; i < count; i++)
            sb.Append(random.Next(0, 10));
        return sb.ToString();
    }

    private static string ComputeCnpjCheckDigits(string baseDigits)
    {
        // Pesos do CNPJ (dígito verificador).
        var firstWeights = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var secondWeights = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        var first = ComputeDigit(baseDigits, firstWeights);
        var second = ComputeDigit(baseDigits + first, secondWeights);
        return first + second;
    }

    private static string ComputeDigit(string digits, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
            sum += (digits[i] - '0') * weights[i];
        var rest = sum % 11;
        return (rest < 2 ? 0 : 11 - rest).ToString();
    }

    private static string ComputeCpfCheckDigits(string baseDigits)
    {
        // Pesos do CPF (dígito verificador).
        var firstWeights = new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var secondWeights = new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        var first = ComputeDigit(baseDigits, firstWeights);
        var second = ComputeDigit(baseDigits + first, secondWeights);
        return first + second;
    }
}