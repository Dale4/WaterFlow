using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WaterFlow.Api.Auth;
using WaterFlow.Api.Data;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Tests;

public class AuthIntegrationTests : IClassFixture<WaterFlowApiFactory>
{
    private const string Password = "Test-pass-1";

    private readonly WaterFlowApiFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(WaterFlowApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ReturnsJwtAcceptedByMe()
    {
        var email = $"operator-{Guid.NewGuid():N}@example.com";
        await CreateUserAsync(email, Password, MemberRole.Operator, TenantIds.NewAvenue, "New Avenue");

        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, Password, null));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Equal(TenantIds.NewAvenue, body.TenantId);
        Assert.Equal(nameof(MemberRole.Operator), body.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        var me = await _client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var document = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal("New Avenue", document.RootElement.GetProperty("tenantName").GetString());
        Assert.Equal(nameof(MemberRole.Operator), document.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsInvalidCredentials()
    {
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        await CreateUserAsync(email, Password, MemberRole.BillingClerk, TenantIds.NewAvenue, "New Avenue");

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "Wrong-pass-1", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", await ReadErrorAsync(response));
    }

    [Fact]
    public async Task Login_FifthFailure_LocksTheAccount()
    {
        var email = $"locked-{Guid.NewGuid():N}@example.com";
        await CreateUserAsync(email, Password, MemberRole.Board, TenantIds.NewAvenue, "New Avenue");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "Wrong-pass-1", null));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, Password, null));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("locked_out", await ReadErrorAsync(locked));
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_OtherTenant_CannotActAsNewAvenue()
    {
        var avenueEmail = $"avenue-{Guid.NewGuid():N}@example.com";
        var otherEmail = $"other-{Guid.NewGuid():N}@example.com";
        var otherTenantId = Guid.NewGuid();
        var avenueUserId = await CreateUserAsync(avenueEmail, Password, MemberRole.Homeowner, TenantIds.NewAvenue, "New Avenue");
        await CreateUserAsync(otherEmail, Password, MemberRole.Operator, otherTenantId, "Other Mutual");

        var asAvenue = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(otherEmail, Password, TenantIds.NewAvenue));
        Assert.Equal(HttpStatusCode.Unauthorized, asAvenue.StatusCode);
        Assert.Equal("invalid_credentials", await ReadErrorAsync(asAvenue));

        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(otherEmail, Password, null));
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Equal(otherTenantId, body.TenantId);

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        var me = await _client.SendAsync(meRequest);
        using var document = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal("Other Mutual", document.RootElement.GetProperty("tenantName").GetString());

        var reset = new HttpRequestMessage(HttpMethod.Post, $"/auth/users/{avenueUserId}/password")
        {
            Content = JsonContent.Create(new SetPasswordRequest("Other-pass-1"))
        };
        reset.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        var resetResponse = await _client.SendAsync(reset);
        Assert.Equal(HttpStatusCode.NotFound, resetResponse.StatusCode);
    }

    private async Task<string> CreateUserAsync(string email, string password, MemberRole role, Guid tenantId, string tenantName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<WaterFlowDbContext>();

        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == tenantId);
        if (tenant is null)
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = tenantName });
            await db.SaveChangesAsync();
        }

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));

        db.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            Role = role
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetString();
    }
}
