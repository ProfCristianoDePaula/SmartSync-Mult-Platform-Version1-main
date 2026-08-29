using Identity.Domain.Common;

namespace Identity.Domain.Entities;

/// <summary>
/// Plan — plano de assinatura de UM módulo da plataforma (Etapa 15). Pertence
/// a exatamente um Module (<see cref="ModuleId"/>); o vínculo do tenant com o
/// módulo é feito via <see cref="TenantModule"/>, que carrega o plano escolhido.
/// Define preço (mensal/anual), período de teste e os limites contratuais do
/// tenant (filiais, usuários e armazenamento).
/// </summary>
public sealed class Plan : Entity<PlanId>
{
    private readonly List<string> _features = [];

    public ModuleId ModuleId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public decimal AnnualPrice { get; private set; }
    public int TrialDays { get; private set; }
    public IReadOnlyList<string> Features => _features.AsReadOnly();

    /// <summary>
    /// Limites contratuais do tenant. <c>null</c> significa "sem limite"
    /// (convenção da plataforma, usada pelo plano de bootstrap "Full Access").
    /// </summary>
    public int? MaxBranches { get; private set; }
    public int? MaxUsers { get; private set; }
    public int? MaxStorageMb { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Plan() { }

    private Plan(
        PlanId id,
        ModuleId moduleId,
        string name,
        string? description,
        decimal monthlyPrice,
        decimal annualPrice,
        int trialDays,
        IReadOnlyList<string> features,
        int? maxBranches,
        int? maxUsers,
        int? maxStorageMb)
        : base(id)
    {
        SetModule(moduleId);
        SetName(name);
        SetDescription(description);
        SetPrices(monthlyPrice, annualPrice);
        SetTrialDays(trialDays);
        SetFeatures(features);
        SetLimits(maxBranches, maxUsers, maxStorageMb);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Plan Create(
        ModuleId moduleId,
        string name,
        string? description,
        decimal monthlyPrice,
        decimal annualPrice,
        int trialDays,
        IReadOnlyList<string> features,
        int? maxBranches,
        int? maxUsers,
        int? maxStorageMb)
        => new(
            PlanId.New(),
            moduleId,
            name,
            description,
            monthlyPrice,
            annualPrice,
            trialDays,
            features,
            maxBranches,
            maxUsers,
            maxStorageMb);

    public void Update(
        string name,
        string? description,
        decimal monthlyPrice,
        decimal annualPrice,
        int trialDays,
        IReadOnlyList<string> features,
        int? maxBranches,
        int? maxUsers,
        int? maxStorageMb)
    {
        SetName(name);
        SetDescription(description);
        SetPrices(monthlyPrice, annualPrice);
        SetTrialDays(trialDays);
        SetFeatures(features);
        SetLimits(maxBranches, maxUsers, maxStorageMb);
    }

    /// <summary>
    /// Soft delete: inativa o plano e registra quando. Planos inativos ficam
    /// fora das consultas (query filter) e não podem ser atribuídos a novos
    /// Tenants.
    /// </summary>
    public void SoftDelete()
    {
        IsActive = false;
        DeletedAtUtc ??= DateTime.UtcNow;
    }

    private void SetModule(ModuleId moduleId)
    {
        if (moduleId == default)
            throw new ArgumentException("O plano precisa pertencer a um módulo.", nameof(moduleId));
        ModuleId = moduleId;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome do plano é obrigatório.", nameof(name));
        Name = name.Trim();
    }

    private void SetDescription(string? description)
        => Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private void SetPrices(decimal monthlyPrice, decimal annualPrice)
    {
        if (monthlyPrice < 0)
            throw new ArgumentException("Preço mensal não pode ser negativo.", nameof(monthlyPrice));
        if (annualPrice < 0)
            throw new ArgumentException("Preço anual não pode ser negativo.", nameof(annualPrice));
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
    }

    private void SetTrialDays(int trialDays)
    {
        if (trialDays < 0)
            throw new ArgumentException("Período de teste não pode ser negativo.", nameof(trialDays));
        TrialDays = trialDays;
    }

    private void SetFeatures(IReadOnlyList<string> features)
    {
        var list = features ?? [];
        var normalized = list.Select(f => f.Trim()).ToList();
        if (normalized.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A lista de features não pode conter itens vazios.", nameof(features));

        _features.Clear();
        _features.AddRange(normalized);
    }

    private void SetLimits(int? maxBranches, int? maxUsers, int? maxStorageMb)
    {
        // Convenção da plataforma: null = "sem limite". Quando um limite é
        // informado, deve ser ao menos 1.
        if (maxBranches is < 1)
            throw new ArgumentException("O limite de filiais deve ser ao menos 1 (ou null para 'sem limite').", nameof(maxBranches));
        if (maxUsers is < 1)
            throw new ArgumentException("O limite de usuários deve ser ao menos 1 (ou null para 'sem limite').", nameof(maxUsers));
        if (maxStorageMb is < 1)
            throw new ArgumentException("O limite de armazenamento deve ser ao menos 1 (ou null para 'sem limite').", nameof(maxStorageMb));
        MaxBranches = maxBranches;
        MaxUsers = maxUsers;
        MaxStorageMb = maxStorageMb;
    }
}
