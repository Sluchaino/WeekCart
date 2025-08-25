using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Data.Configurations;

namespace Infrastructure.Data
{
    public class ProductDbContext : DbContext
    {
        public DbSet<Product> Products => Set<Product>();

        public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.ApplyConfiguration(new ProductConfiguration());
        }
    }
}
