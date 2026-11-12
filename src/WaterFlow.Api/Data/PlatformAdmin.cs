using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Data;

public class PlatformAdmin
{
    public string UserId { get; set; } = "";

    public ApplicationUser User { get; set; } = null!;
}
