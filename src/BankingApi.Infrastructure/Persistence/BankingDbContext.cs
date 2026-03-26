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
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanApprovalHistory> LoanApprovalHistories => Set<LoanApprovalHistory>();

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

        modelBuilder.Entity<Loan>(e =>
        {
            e.HasDiscriminator<string>("LoanType")
            .HasValue<PersonalLoan>("Personal")
            .HasValue<PayrollLoan>("Payroll");

            e.HasKey(l => l.Id);
            e.Property(l => l.ClientId).IsRequired().HasMaxLength(450);
            e.Property(l => l.Amount).HasPrecision(18, 2);
            e.Property(l => l.InterestRate).HasPrecision(8, 4);
            e.Property(l => l.MonthlyPayment).HasPrecision(18, 2);
            e.Property(l => l.RequiredApprovalRole).IsRequired().HasMaxLength(50);
            e.Property(l => l.ApprovedBy).HasMaxLength(450);
            e.Property(l => l.RejectionReason).HasMaxLength(1000);
            e.HasIndex(l => l.ClientId);
            e.HasIndex(l => l.Status);
            e.Property(l => l.AiAnalysisStatus).HasConversion<int>();
            e.Property(l => l.AiAnalysisRequestedAt);



            e.HasMany(l => l.ApprovalHistory)
            .WithOne()
            .HasForeignKey(h => h.LoanId)
            .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PayrollLoan>(e =>
        {
            e.Property(l => l.EmployerName).HasMaxLength(200);
            e.Property(l => l.MonthlySalary).HasPrecision(18, 2);
            e.Property(l => l.ExistingPayrollDeductions).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LoanApprovalHistory>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.UserId).IsRequired().HasMaxLength(450);
            e.Property(h => h.Role).IsRequired().HasMaxLength(50);
            e.Property(h => h.Comment).HasMaxLength(1000);
            e.HasIndex(h => h.LoanId);
        });
    }
}
