using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> b)
        {
            b.ToTable("products");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.SellerId);

            b.Property(x => x.Title).IsRequired().HasMaxLength(120);
            b.Property(x => x.Price).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("RUB");
            b.Property(x => x.ImageUrl).HasMaxLength(2048).IsUnicode(false);

            b.HasQueryFilter(x => !x.IsArchived);
        }
    }
}
