using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WaterFlow.Api.Data;

namespace WaterFlow.Api.Tests;

public static class TestJwt
{
    public const string SigningKey = "waterflow-test-signing-key-32chars-min";

    public const string Issuer = "WaterFlow.Tests";

    public const string Audience = "WaterFlow.Tests";
}

public static class SqliteTestHost
{
    public static SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source=file:waterflow-{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        return connection;
    }

    public static void Configure(IWebHostBuilder builder, SqliteConnection connection)
    {
        builder.UseSetting("ConnectionStrings:WaterFlow", "Host=localhost;Database=waterflow_tests;Username=waterflow;Password=unused");
        builder.UseSetting("Jwt:SigningKey", TestJwt.SigningKey);
        builder.UseSetting("Jwt:Issuer", TestJwt.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwt.Audience);

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<WaterFlowDbContext>)
                    || descriptor.ServiceType == typeof(WaterFlowDbContext)
                    || (descriptor.ServiceType.IsGenericType
                        && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)
                        && descriptor.ServiceType.GenericTypeArguments[0] == typeof(WaterFlowDbContext)))
                .ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<WaterFlowDbContext>(options => options.UseSqlite(connection));
        });
    }
}
