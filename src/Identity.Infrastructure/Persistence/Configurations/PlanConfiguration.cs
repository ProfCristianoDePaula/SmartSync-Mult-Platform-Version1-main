using Identity.Domain.Common;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => PlanId.From(value))
            .ValueGeneratedNever();

        // Plano pertence a um módulo (Etapa 15): FK obrigatória para modules.id.
        builder.Property(x => x.ModuleId)
            .HasColumnName("module_id")
            .HasConversion(id => id.Value, value => ModuleId.From(value))
            .IsRequired();

        builder.HasOne<Module>()
            .WithMany()
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(x => x.MonthlyPrice)
            .HasColumnName("monthly_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.AnnualPrice)
            .HasColumnName("annual_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TrialDays)
            .HasColumnName("trial_days")
            .IsRequired();

        // Lista de features como array nativo do Postgres (text[]).
        builder.PrimitiveCollection(x => x.Features)
            .HasColumnName("features");

        // Limites contratuais NULLABLE: null = "sem limite" (convenção da
        // plataforma, usada pelo plano de bootstrap "Full Access").
        builder.Property(x => x.MaxBranches)
            .HasColumnName("max_branches");

        builder.Property(x => x.MaxUsers)
            .HasColumnName("max_users");

        builder.Property(x => x.MaxStorageMb)
            .HasColumnName("max_storage_mb");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.HasIndex(x => x.ModuleId)
            .HasDatabaseName("IX_plans_module_id");

        // Nome único por MÓDULO entre planos ATIVOS (Etapa 15): o mesmo nome
        // pode existir em módulos diferentes, mas não duas vezes dentro do
        // mesmo módulo. Índice parcial no Postgres: um plano inativo/soft-deletado
        // não bloqueia a reutilização do nome.
        builder.HasIndex(x => new { x.ModuleId, x.Name })
            .IsUnique()
            .HasFilter("\"is_active\"")
            .HasDatabaseName("IX_plans_module_id_name_active");

        // Soft delete (Etapa 12) via named query filter: por padrão, planos
        // inativos ficam fora das consultas. ListPlans(includeInactive) usa
        // IgnoreQueryFilters("Active").
        builder.HasQueryFilter("Active", p => p.IsActive);
    }
}
