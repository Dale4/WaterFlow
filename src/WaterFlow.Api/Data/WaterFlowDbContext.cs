using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Data;

public class WaterFlowDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly CurrentTenant _currentTenant;

    public WaterFlowDbContext(DbContextOptions<WaterFlowDbContext> options, CurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>().HasData(new Tenant
        {
            Id = TenantIds.NewAvenue,
            Name = "New Avenue"
        });

        builder.Entity<Membership>(entity =>
        {
            entity.Property(membership => membership.Role)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.HasIndex(membership => new { membership.UserId, membership.TenantId })
                .IsUnique();
            entity.HasOne(membership => membership.User)
                .WithMany()
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(membership => membership.Tenant)
                .WithMany(tenant => tenant.Memberships)
                .HasForeignKey(membership => membership.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        foreach (var type in typeof(WaterFlowDbContext).Assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !typeof(ITenantOwned).IsAssignableFrom(type))
                continue;

            GetType()
                .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(type)
                .Invoke(this, [builder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantOwned
    {
        builder.Entity<TEntity>().HasQueryFilter(entity => entity.TenantId == _currentTenant.Id);
    }
}
