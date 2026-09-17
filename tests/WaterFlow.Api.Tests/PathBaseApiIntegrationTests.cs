using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WaterFlow.Api.Tests;

public sealed class WaterFlowPathBaseApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_PATHBASE"] = "/waterflow"
            });
        });
    }
}

public class PathBaseApiIntegrationTests : IClassFixture<WaterFlowPathBaseApiFactory>
{
    private readonly HttpClient _client;

    public PathBaseApiIntegrationTests(WaterFlowPathBaseApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHelloUnderPathBase_ReturnsGreeting()
    {
        var response = await _client.GetAsync("/waterflow/hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello from WaterFlow", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetHelloWithoutPathBase_StillWorks()
    {
        var response = await _client.GetAsync("/hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello from WaterFlow", await response.Content.ReadAsStringAsync());
    }
}
