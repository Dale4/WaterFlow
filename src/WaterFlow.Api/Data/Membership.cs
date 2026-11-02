using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Data;

public class Membership
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = "";

    public ApplicationUser User { get; set; } = null!;

    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public MemberRole Role { get; set; }
}
