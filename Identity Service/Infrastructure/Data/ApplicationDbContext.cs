using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public ApplicationDbContext(DbContextOptions opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                   .HasQueryFilter(u => !u.IsDeleted);   // скрываем удалённых

            builder.Entity<RefreshToken>()
                   .HasIndex(rt => rt.UserId);
        }
    }
}
