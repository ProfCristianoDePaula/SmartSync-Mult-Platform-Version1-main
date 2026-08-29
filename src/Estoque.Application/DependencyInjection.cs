using Estoque.Application.AutoCompra;
using Estoque.Application.Catalogo;
using Estoque.Application.Integracao;
using Estoque.Application.IntegrationServices;
using Estoque.Application.Movimentacoes;
using Estoque.Application.Politicas;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Estoque.Application;

/// <summary>
/// Registro dos serviços da camada Application (padrão do Identity):
/// validators FluentValidation descobertos por assembly. Os serviços concretos
/// são registrados pela Infrastructure.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddEstoqueApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
