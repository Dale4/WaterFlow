using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WaterFlow.Api.Data;

public sealed class WaterFlowDbContextFactory : IDesignTimeDbContextFactory<WaterFlowDbContext>
{
    public WaterFlowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WaterFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=waterflow;Username=waterflow;Password=waterflow")
            .Options;
        return new WaterFlowDbContext(options, new CurrentTenant());
    }
}
