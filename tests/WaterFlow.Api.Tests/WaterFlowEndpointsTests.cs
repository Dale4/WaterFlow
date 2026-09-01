namespace WaterFlow.Api.Tests;

public class WaterFlowEndpointsTests
{
    [Fact]
    public void GetStatus_ReturnsWaterFlowRunning()
    {
        var status = WaterFlowEndpoints.GetStatus();

        Assert.Equal("WaterFlow", status.Service);
        Assert.Equal("running", status.Status);
    }

    [Fact]
    public void GetHello_ReturnsGreeting()
    {
        Assert.Equal("Hello from WaterFlow", WaterFlowEndpoints.GetHello());
    }
}
