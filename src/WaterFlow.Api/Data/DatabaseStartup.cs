using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Data;

public static class DatabaseStartup
{
    public static async Task ApplyAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaterFlowDbContext>();
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            await db.Database.EnsureCreatedAsync();
        else
            await db.Database.MigrateAsync();

        if (!await db.Tenants.AnyAsync(tenant => tenant.Id == TenantIds.NewAvenue))
        {
            db.Tenants.Add(new Tenant { Id = TenantIds.NewAvenue, Name = "New Avenue" });
            await db.SaveChangesAsync();
        }

        await SeedOperatorAsync(scope.ServiceProvider, db);
    }

    private static async Task SeedOperatorAsync(IServiceProvider services, WaterFlowDbContext db)
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
                throw new InvalidOperationException($"Could not seed the operator account. {reasons}");
            }
        }

        var alreadyMember = await db.Memberships.AnyAsync(membership =>
            membership.UserId == user.Id && membership.TenantId == TenantIds.NewAvenue);
        if (alreadyMember)
            return;

        db.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = TenantIds.NewAvenue,
            Role = MemberRole.Operator
        });
        await db.SaveChangesAsync();
    }
}
