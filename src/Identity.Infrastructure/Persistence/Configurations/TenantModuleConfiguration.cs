using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantModuleConfiguration : IEntityTypeConfiguration<TenantModule>
{
    public void Configure(EntityTypeBuilder<TenantModule> builder)
    {
        builder.ToTable("tenant_modules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => TenantModuleId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(x => x.ModuleId)
            .HasColumnName("module_id")
            .HasConversion(id => id.Value, value => ModuleId.From(value))
            .IsRequired();

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id")
            .HasConversion(id => id.Value, value => PlanId.From(value))
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.StartDateUtc)
            .HasColumnName("start_date_utc")
            .IsRequired();

        builder.Property(x => x.EndDateUtc)
            .HasColumnName("end_date_utc");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Module>()
            .WithMany()
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_tenant_modules_tenant_id");

        builder.HasIndex(x => x.ModuleId)
            .HasDatabaseName("IX_tenant_modules_module_id");

        builder.HasIndex(x => x.PlanId)
            .HasDatabaseName("IX_tenant_modules_plan_id");

        // Regra central (Etapa 15): no máximo UM vínculo ATIVO por (tenant, module).
        // Histórico (status = 2) não bloqueia novas vigências.
        builder.HasIndex(x => new { x.TenantId, x.ModuleId })
            .HasDatabaseName("UQ_tenant_modules_tenant_module_active")
            .IsUnique()
            .HasFilter("\"status\" = 1");
    }
}
