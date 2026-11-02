using Npgsql;

namespace WaterFlow.Api.Data;

public static class WaterFlowConnection
{
    public const string Name = "WaterFlow";

    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString(Name);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var host = configuration["DB_HOST"];
        var database = configuration["DB_NAME"];
        var username = configuration["DB_USER"];
        var password = configuration["DB_PASSWORD"];
        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings:WaterFlow in User Secrets, or DB_HOST, DB_NAME, and DB_USER.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = database,
            Username = username,
            Password = password ?? ""
        };
        if (int.TryParse(configuration["DB_PORT"], out var port))
            builder.Port = port;

        return builder.ConnectionString;
    }
}
