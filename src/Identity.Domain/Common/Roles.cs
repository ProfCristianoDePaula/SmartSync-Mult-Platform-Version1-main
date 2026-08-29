namespace Identity.Domain.Common;

/// <summary>
/// Roles of the platform (constant names, sem dependência de ASP.NET Identity).
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Manager = "Manager";
    public const string Seller = "Seller";
    public const string Delivery = "Delivery";
    public const string Client = "Client";

    /// <summary>All platform roles, in seed order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        SuperAdmin,
        TenantAdmin,
        Manager,
        Seller,
        Delivery,
        Client
    ];
}