using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

public interface IUserActivityLogger
{
    Task LogAsync(Guid tenantId, Guid userId, string action, string module,
        string description, Guid? entityId = null,
        string? ipAddress = null, string? userAgent = null);
}

public class UserActivityLogger : IUserActivityLogger
{
    private readonly AppDbContext _db;

    public UserActivityLogger(AppDbContext db) => _db = db;

    public async Task LogAsync(Guid tenantId, Guid userId, string action, string module,
        string description, Guid? entityId = null,
        string? ipAddress = null, string? userAgent = null)
    {
        _db.UserActivityLogs.Add(new UserActivityLog
        {
            TenantId    = tenantId,
            UserId      = userId,
            Action      = action,
            Module      = module,
            Description = description,
            EntityId    = entityId,
            IpAddress   = ipAddress,
            UserAgent   = userAgent
        });
        await _db.SaveChangesAsync();
    }
}
