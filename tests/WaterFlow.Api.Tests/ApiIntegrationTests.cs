using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WaterFlow.Api.Tests;

public class ApiIntegrationTests : IClassFixture<WaterFlowApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WaterFlowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRoot_ReturnsServiceStatusJson()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var status = await response.Content.ReadFromJsonAsync<ServiceStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal("WaterFlow", status.Service);
        Assert.Equal("running", status.Status);
    }

    [Fact]
    public async Task GetHello_ReturnsGreeting()
    {
        var response = await _client.GetAsync("/hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello from WaterFlow", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetHealth_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetOpenApi_ReturnsDocumentWithHelloPath()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("openapi", out _));
        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/hello", out _));
    }

    [Fact]
    public async Task GetMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
