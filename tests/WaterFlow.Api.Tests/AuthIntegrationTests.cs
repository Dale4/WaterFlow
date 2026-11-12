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

    [Fact]
    public async Task Login_PlatformAdminWithoutTenant_ReturnsPlatformToken()
    {
        var email = $"platform-{Guid.NewGuid():N}@example.com";
        await CreatePlatformAdminAsync(email, Password);

        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, Password, null));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Null(body.TenantId);
        Assert.Null(body.Role);
        Assert.Equal(AuthClaimTypes.PlatformAdmin, body.PlatformRole);

        var me = await SendAsync(HttpMethod.Get, "/auth/me", body.Token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var document = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal(AuthClaimTypes.PlatformAdmin, document.RootElement.GetProperty("platformRole").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("tenantId").ValueKind);

        var changed = await SendAsync(
            HttpMethod.Post,
            "/auth/change-password",
            body.Token,
            new ChangePasswordRequest(Password, "Test-pass-2"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
    }

    [Fact]
    public async Task Login_PlatformAdminWithTenantId_ReturnsThatMembership()
    {
        var email = $"both-{Guid.NewGuid():N}@example.com";
        var userId = await CreatePlatformAdminAsync(email, Password);
        var tenantId = Guid.NewGuid();
        await AddMembershipAsync(userId, tenantId, $"Both {Guid.NewGuid():N}", MemberRole.Operator);

        var asTenant = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, Password, tenantId));
        Assert.Equal(HttpStatusCode.OK, asTenant.StatusCode);
        var tenantBody = await asTenant.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(tenantBody);
        Assert.Equal(tenantId, tenantBody.TenantId);
        Assert.Equal(nameof(MemberRole.Operator), tenantBody.Role);
        Assert.Null(tenantBody.PlatformRole);

        var asPlatform = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, Password, null));
        var platformBody = await asPlatform.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(platformBody);
        Assert.Equal(AuthClaimTypes.PlatformAdmin, platformBody.PlatformRole);
        Assert.Null(platformBody.TenantId);
    }

    [Fact]
    public async Task CreateTenant_OperatorCanLogIn_AndAnotherOperatorCanBeAdded()
    {
        var adminEmail = $"admin-{Guid.NewGuid():N}@example.com";
        await CreatePlatformAdminAsync(adminEmail, Password);
        var adminLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(adminEmail, Password, null));
        var admin = await adminLogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(admin);

        var tenantName = $"New Avenue {Guid.NewGuid():N}";
        var operatorName = $"op-{Guid.NewGuid():N}@example.com";
        var create = await SendAsync(
            HttpMethod.Post,
            "/tenants",
            admin.Token,
            new CreateTenantRequest(tenantName, operatorName, Password));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreatedTenantResponse>();
        Assert.NotNull(created);
        Assert.Equal(tenantName, created.Name);

        var operatorLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(operatorName, Password, null));
        Assert.Equal(HttpStatusCode.OK, operatorLogin.StatusCode);
        var op = await operatorLogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(op);
        Assert.Equal(created.TenantId, op.TenantId);
        Assert.Equal(nameof(MemberRole.Operator), op.Role);

        var duplicate = await SendAsync(
            HttpMethod.Post,
            "/tenants",
            admin.Token,
            new CreateTenantRequest(tenantName, $"other-{Guid.NewGuid():N}@example.com", Password));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("tenant_name_in_use", await ReadErrorAsync(duplicate));

        var denied = await SendAsync(
            HttpMethod.Post,
            "/tenants",
            op.Token,
            new CreateTenantRequest($"Other {Guid.NewGuid():N}", $"nope-{Guid.NewGuid():N}@example.com", Password));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var secondName = $"second-{Guid.NewGuid():N}@example.com";
        var add = await SendAsync(
            HttpMethod.Post,
            $"/tenants/{created.TenantId}/operators",
            admin.Token,
            new AddOperatorRequest(secondName, Password));
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var secondLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(secondName, Password, created.TenantId));
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);

        var existing = $"existing-{Guid.NewGuid():N}@example.com";
        await CreateUserAsync(existing, Password, MemberRole.Homeowner, Guid.NewGuid(), $"Elsewhere {Guid.NewGuid():N}");
        var linked = await SendAsync(
            HttpMethod.Post,
            $"/tenants/{created.TenantId}/operators",
            admin.Token,
            new AddOperatorRequest(existing, "Different-pass-1"));
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);

        var unchanged = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(existing, Password, created.TenantId));
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        var rejected = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(existing, "Different-pass-1", created.TenantId));
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private async Task<string> CreatePlatformAdminAsync(string email, string password)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<WaterFlowDbContext>();

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));

        db.PlatformAdmins.Add(new PlatformAdmin { UserId = user.Id });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task AddMembershipAsync(string userId, Guid tenantId, string tenantName, MemberRole role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaterFlowDbContext>();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = tenantName });
        db.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Role = role
        });
        await db.SaveChangesAsync();
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
