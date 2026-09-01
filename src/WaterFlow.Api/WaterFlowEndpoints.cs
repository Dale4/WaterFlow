namespace WaterFlow.Api;

public sealed record ServiceStatusResponse(string Service, string Status);

public static class WaterFlowEndpoints
{
    public static ServiceStatusResponse GetStatus() => new("WaterFlow", "running");

    public static string GetHello() => "Hello from WaterFlow";
}
