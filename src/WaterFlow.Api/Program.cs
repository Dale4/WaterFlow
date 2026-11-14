using WaterFlow.Api;
using WaterFlow.Api.Auth;
using WaterFlow.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddWaterFlowData(builder.Configuration);
builder.Services.AddWaterFlowAuth(builder.Configuration);

var app = builder.Build();

var pathBase = app.Configuration["ASPNETCORE_PATHBASE"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CurrentTenantMiddleware>();

app.MapOpenApi();

app.MapGet("/", () => Results.Ok(WaterFlowEndpoints.GetStatus()));

app.MapGet("/hello", WaterFlowEndpoints.GetHello);

app.MapGet("/hello2", WaterFlowEndpoints.GetHello2);

app.MapHealthChecks("/health");

app.MapAuthEndpoints();
app.MapTenantEndpoints();
app.MapPlatformAdminEndpoints();

await DatabaseStartup.ApplyAsync(app);

app.Run();

public partial class Program { }
