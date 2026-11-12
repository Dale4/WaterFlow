using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Data;

public static class DatabaseStartup
{
    public static async Task ApplyAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaterFlowDbContext>();
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            await EnsureSchemaAsync(db);
        else
            await db.Database.MigrateAsync();

        await SeedPlatformAdminAsync(scope.ServiceProvider, db);
    }

    private static async Task EnsureSchemaAsync(WaterFlowDbContext db)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync())
            await creator.CreateAsync();

        if (!await creator.HasTablesAsync())
            await creator.CreateTablesAsync();
    }

    private static async Task SeedPlatformAdminAsync(IServiceProvider services, WaterFlowDbContext db)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var email = configuration["Seed:OperatorEmail"];
        var password = configuration["Seed:OperatorPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                var reasons = string.Join(" ", created.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Could not seed the platform admin account. {reasons}");
            }
        }

        var alreadyAdmin = await db.PlatformAdmins.AnyAsync(admin => admin.UserId == user.Id);
        if (alreadyAdmin)
            return;

        db.PlatformAdmins.Add(new PlatformAdmin { UserId = user.Id });
        await db.SaveChangesAsync();
    }
}
