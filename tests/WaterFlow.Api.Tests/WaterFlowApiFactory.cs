using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace WaterFlow.Api.Tests;

public sealed class WaterFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = SqliteTestHost.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        SqliteTestHost.Configure(builder, _connection);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
