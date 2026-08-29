using Identity.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class PhoneVerificationCodeConfiguration : IEntityTypeConfiguration<PhoneVerificationCode>
{
    public void Configure(EntityTypeBuilder<PhoneVerificationCode> builder)
    {
        builder.ToTable("phone_verification_codes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CodeHash)
            .HasColumnName("code_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.Property(x => x.UsedAtUtc)
            .HasColumnName("used_at_utc");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_phone_verification_codes_user_id");
    }
}
