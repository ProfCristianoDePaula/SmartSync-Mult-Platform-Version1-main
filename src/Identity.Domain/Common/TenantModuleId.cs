namespace Identity.Domain.Common;

public readonly record struct TenantModuleId(Guid Value)
{
    public static TenantModuleId New() => new(Guid.NewGuid());
    public static TenantModuleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
