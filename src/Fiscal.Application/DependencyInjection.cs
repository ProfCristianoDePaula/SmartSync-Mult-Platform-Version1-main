using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Fiscal.Application;

/// <summary>
/// Registro dos serviços da camada Application (padrão do Identity/Estoque):
/// validators FluentValidation descobertos por assembly. Os serviços concretos
/// são registrados pela Infrastructure.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFiscalApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
