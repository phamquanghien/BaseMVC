using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BaseMVC.Infrastructure.Persistence;

/// <summary>Provides the SQLite provider when EF tooling builds migrations outside the running application.</summary>
public sealed class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=basemvc-design-time.db")
            .Options;
        return new ApplicationDbContext(options);
    }
}
