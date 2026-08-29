namespace Identity.Domain.Common;

public readonly record struct PlanId(Guid Value)
{
    public static PlanId New() => new(Guid.NewGuid());
    public static PlanId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
