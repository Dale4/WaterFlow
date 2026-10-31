using Microsoft.EntityFrameworkCore;

namespace WaterFlow.Api.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddWaterFlowData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = WaterFlowConnection.Resolve(configuration);
        services.AddDbContext<WaterFlowDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<CurrentTenant>();
        return services;
    }
}
