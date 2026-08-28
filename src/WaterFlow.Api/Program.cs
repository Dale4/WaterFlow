var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/", () => Results.Ok(new { service = "WaterFlow", status = "running" }));

app.MapGet("/hello", () => "Hello from WaterFlow");

app.MapHealthChecks("/health");

app.Run();
