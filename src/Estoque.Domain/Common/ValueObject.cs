namespace Estoque.Domain.Common;

/// <summary>
/// Base class for value objects, providing structural equality.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityValues();

    public bool Equals(ValueObject? other)
        => other is not null
           && GetType() == other.GetType()
           && GetEqualityValues().SequenceEqual(other.GetEqualityValues());

    public override bool Equals(object? obj) => obj is ValueObject vo && Equals(vo);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var v in GetEqualityValues())
            hash.Add(v);
        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !(left == right);

    public override string ToString() => string.Join("|", GetEqualityValues());
}
