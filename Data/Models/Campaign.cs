namespace TelecallingCRM.Data.Models;

public enum CampaignStatus { Draft, Active, Paused, Completed, Archived }

public enum CampaignType
{
    ColdCalling,
    Election,
    HotelBooking,
    Insurance,
    Education,
    RealEstate,
    FollowUp,
    Upsell,
    WinBack,
    Survey,
    Other
}

public class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public CampaignType Type { get; set; } = CampaignType.ColdCalling;
    public string? Script { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int TargetCallsPerDay { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Lead> Leads { get; set; } = new List<Lead>();
}

