using WaterFlow.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/", () => Results.Ok(WaterFlowEndpoints.GetStatus()));

app.MapGet("/hello", WaterFlowEndpoints.GetHello);

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
