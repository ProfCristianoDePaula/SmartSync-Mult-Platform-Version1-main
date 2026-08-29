using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application;

/// <summary>
/// Registro dos serviços da camada Application. Hoje são apenas os validators
/// FluentValidation (descobertos por assembly); o restante dos serviços é
/// registrado pela Infrastructure (<see cref="Identity.Infrastructure.DependencyInjection"/>).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
