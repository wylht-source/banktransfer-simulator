using BankingApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Tests.Integration;

public static class BankingDbContextFactory
{
    public static BankingDbContext Create()
    {
        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new BankingDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}