namespace Estoque.Domain.Common;

/// <summary>
/// Contrato para agregados que coletam eventos de domínio. Os eventos são
/// despachados pós-commit pelos serviços de aplicação (nunca antes da
/// persistência ter sucesso).
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<object> DomainEvents { get; }
    void Raise(object domainEvent);
    IReadOnlyList<object> PopEvents();
}
