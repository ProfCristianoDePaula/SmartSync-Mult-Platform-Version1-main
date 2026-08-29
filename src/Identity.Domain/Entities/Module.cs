using System.Text.RegularExpressions;
using Identity.Domain.Common;

namespace Identity.Domain.Entities;

/// <summary>
/// Module — produto/serviço da plataforma (ex.: "SmartSync Agro",
/// "SmartSync E-Commerce", "SmartSync Agenda") que um Tenant pode contratar.
///
/// O <see cref="Slug"/> é o identificador ESTÁVEL e programático usado pelos
/// demais microsserviços (não muda mesmo se o nome de exibição mudar), por isso
/// é imutável após a criação.
/// </summary>
public sealed class Module : Entity<ModuleId>
{
    private static readonly Regex SlugPattern = new(
        @"^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string? Description { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Module() { }

    private Module(ModuleId id, string name, string slug, string? description)
        : base(id)
    {
        SetName(name);
        SetSlug(slug);
        SetDescription(description);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Module Create(string name, string slug, string? description)
        => new(ModuleId.New(), name, slug, description);

    /// <summary>Edita apenas nome/descrição — o Slug é imutável (identidade).</summary>
    public void Update(string name, string? description)
    {
        SetName(name);
        SetDescription(description);
    }

    public void SoftDelete()
    {
        if (!IsActive)
            return;

        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome do módulo é obrigatório.", nameof(name));
        Name = name.Trim();
    }

    private void SetSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug do módulo é obrigatório.", nameof(slug));

        var normalized = slug.Trim().ToLowerInvariant();
        if (!SlugPattern.IsMatch(normalized))
            throw new ArgumentException(
                "Slug inválido: use apenas letras minúsculas, números e hífens (ex.: \"agro\", \"ecommerce\", \"agenda\").",
                nameof(slug));

        Slug = normalized;
    }

    private void SetDescription(string? description)
        => Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
