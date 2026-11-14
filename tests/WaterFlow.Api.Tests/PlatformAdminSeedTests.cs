using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WaterFlow.Api.Auth;
using WaterFlow.Api.Data;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Tests;

public class PlatformAdminSeedTests
{
    private const string SeedEmail = "seed-admin@example.com";
    private const string Password = "Test-pass-1";

    [Fact]
    public async Task Seed_StaysGone_WhileAnotherAdminRemains_AndReturnsWhenNoneRemain()
    {
        await using var connection = SqliteTestHost.Open();
        var replacementEmail = $"replacement-{Guid.NewGuid():N}@example.com";

        await using (var first = new SeedApiFactory(connection))
        {
            var client = first.CreateClient();
            var seedLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(SeedEmail, Password, null));
            Assert.Equal(HttpStatusCode.OK, seedLogin.StatusCode);
            var seed = await seedLogin.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(seed);

            var me = await client.SendAsync(Authorized(HttpMethod.Get, "/auth/me", seed.Token));
            using var document = System.Text.Json.JsonDocument.Parse(await me.Content.ReadAsStringAsync());
            var seedUserId = document.RootElement.GetProperty("userId").GetString();
            Assert.False(string.IsNullOrWhiteSpace(seedUserId));

            var create = await client.SendAsync(Authorized(
                HttpMethod.Post,
                "/platform-admins",
                seed.Token,
                new CreatePlatformAdminRequest(replacementEmail, Password)));
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);

            var delete = await client.SendAsync(Authorized(HttpMethod.Delete, $"/platform-admins/{seedUserId}", seed.Token));
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        await using (var second = new SeedApiFactory(connection))
        {
            var client = second.CreateClient();
            var seedAgain = await client.PostAsJsonAsync("/auth/login", new LoginRequest(SeedEmail, Password, null));
            Assert.Equal(HttpStatusCode.Unauthorized, seedAgain.StatusCode);

            var replacement = await client.PostAsJsonAsync("/auth/login", new LoginRequest(replacementEmail, Password, null));
            Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
            var replacementBody = await replacement.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(replacementBody);

            var me = await client.SendAsync(Authorized(HttpMethod.Get, "/auth/me", replacementBody.Token));
            using var meDocument = System.Text.Json.JsonDocument.Parse(await me.Content.ReadAsStringAsync());
            var replacementUserId = meDocument.RootElement.GetProperty("userId").GetString();
            var last = await client.SendAsync(Authorized(HttpMethod.Delete, $"/platform-admins/{replacementUserId}", replacementBody.Token));
            Assert.Equal(HttpStatusCode.Conflict, last.StatusCode);
            using var errorDocument = System.Text.Json.JsonDocument.Parse(await last.Content.ReadAsStringAsync());
            Assert.Equal("last_platform_admin", errorDocument.RootElement.GetProperty("error").GetString());

            await using var scope = second.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WaterFlowDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admins = await db.PlatformAdmins.ToListAsync();
            db.PlatformAdmins.RemoveRange(admins);
            await db.SaveChangesAsync();
            foreach (var admin in admins)
            {
                var user = await users.FindByIdAsync(admin.UserId);
                if (user is not null)
                    await users.DeleteAsync(user);
            }
        }

        await using var third = new SeedApiFactory(connection);
        var restored = await third.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(SeedEmail, Password, null));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private sealed class SeedApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection;

        public SeedApiFactory(SqliteConnection connection) => _connection = connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            SqliteTestHost.Configure(builder, _connection);
            builder.UseSetting("Seed:OperatorEmail", SeedEmail);
            builder.UseSetting("Seed:OperatorPassword", Password);
        }
    }
}
