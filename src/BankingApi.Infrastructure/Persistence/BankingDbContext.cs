using BankingApi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Infrastructure.Persistence;

public class BankingDbContext : IdentityDbContext<IdentityUser>
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Required for Identity tables

        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.AccountNumber).IsRequired().HasMaxLength(50);
            e.Property(a => a.OwnerName).IsRequired().HasMaxLength(200);
            e.Property(a => a.OwnerId).IsRequired().HasMaxLength(450);
            e.Property(a => a.Balance).HasPrecision(18, 2);
            e.HasIndex(a => a.AccountNumber).IsUnique();
            e.HasIndex(a => a.OwnerId);

            e.HasMany(a => a.Transactions)
             .WithOne()
             .HasForeignKey(t => t.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Description).HasMaxLength(500);
            e.HasIndex(t => t.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            e.HasIndex(t => new { t.AccountId, t.CreatedAt });
        });
    }
}