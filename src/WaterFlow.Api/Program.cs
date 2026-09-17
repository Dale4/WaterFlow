using WaterFlow.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

var pathBase = app.Configuration["ASPNETCORE_PATHBASE"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

app.MapOpenApi();

app.MapGet("/", () => Results.Ok(WaterFlowEndpoints.GetStatus()));

app.MapGet("/hello", WaterFlowEndpoints.GetHello);

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
