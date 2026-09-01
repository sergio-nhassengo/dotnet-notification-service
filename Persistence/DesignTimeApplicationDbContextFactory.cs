using Application.Common.Interfaces;
using Application.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Persistence;

public sealed class DesignTimeApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MPDCApiTemplate;Trusted_Connection=True")
            .Options;
        return new ApplicationDbContext(options, new DesignClock(), new DesignUser());
    }
    private sealed class DesignClock : IDateTime { public DateTimeOffset Now => DateTimeOffset.UtcNow; }
    private sealed class DesignUser : ICurrentUserService
    {
        public string? UserId => "migration";
        public IReadOnlyList<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
