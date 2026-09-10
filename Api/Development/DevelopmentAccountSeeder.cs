using Application.Common.Security;
using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace MPDCApiTemplate.Development;

public static class DevelopmentAccountSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        var settings = app.Configuration.GetSection("DevelopmentAccount");
        var userName = settings["UserName"];
        var password = settings["Password"];
        var email = settings["Email"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Preserve existing accounts and passwords on subsequent starts.
        if (await db.Users.AnyAsync(user => user.UserName == userName || user.Email == email)) return;

        var role = await db.Roles.SingleAsync(role => role.Name == RoleNames.Admin);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Users.Add(new User
        {
            UserName = userName,
            Email = email,
            FirstName = "Development",
            LastName = "Admin",
            PasswordHash = hasher.Hash(password),
            RoleId = role.Id
        });
        await db.SaveChangesAsync();
    }
}
