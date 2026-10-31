namespace WaterFlow.Api.Data;

public class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public ICollection<Membership> Memberships { get; set; } = [];
}
