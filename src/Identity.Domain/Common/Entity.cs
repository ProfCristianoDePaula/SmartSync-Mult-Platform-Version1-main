namespace Identity.Domain.Common;

/// <summary>
/// Base for entities with a strongly-typed identifier.
/// </summary>
/// <typeparam name="TId">Strongly-typed identifier type.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; } = default!;

    protected Entity(TId id) => Id = id;

    protected Entity() { }

    public bool Equals(Entity<TId>? other)
        => other is not null && EqualityComparer<TId>.Default.Equals(Id, other.Id)
                              && GetType() == other.GetType();

    public override bool Equals(object? obj) => obj is Entity<TId> e && Equals(e);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}