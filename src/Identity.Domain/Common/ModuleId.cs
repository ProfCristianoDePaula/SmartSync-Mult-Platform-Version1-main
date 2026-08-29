namespace Identity.Domain.Common;

public readonly record struct ModuleId(Guid Value)
{
    public static ModuleId New() => new(Guid.NewGuid());
    public static ModuleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
