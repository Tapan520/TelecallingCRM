using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

public interface ITenantModuleService
{
    /// <summary>Returns the set of enabled modules for a given tenant (all enabled if no overrides).</summary>
    Task<HashSet<CrmModule>> GetEnabledModulesAsync(Guid tenantId);

    /// <summary>Returns true when the module is enabled for the tenant.</summary>
    Task<bool> IsEnabledAsync(Guid tenantId, CrmModule module);

    /// <summary>Replaces the full module configuration for a tenant.</summary>
    Task SetModulesAsync(Guid tenantId, IEnumerable<CrmModule> enabledModules);
}

public class TenantModuleService : ITenantModuleService
{
    private readonly AppDbContext _db;

    public TenantModuleService(AppDbContext db) => _db = db;

    public async Task<HashSet<CrmModule>> GetEnabledModulesAsync(Guid tenantId)
    {
        var rows = await _db.TenantModuleAccess
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .ToListAsync();

        // No overrides at all ? all modules on
        if (rows.Count == 0)
            return new HashSet<CrmModule>(Enum.GetValues<CrmModule>());

        // Return only the enabled ones
        return rows.Where(r => r.IsEnabled).Select(r => r.Module).ToHashSet();
    }

    public async Task<bool> IsEnabledAsync(Guid tenantId, CrmModule module)
    {
        var row = await _db.TenantModuleAccess
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Module == module);

        // Missing row ? enabled by default
        return row?.IsEnabled ?? true;
    }

    public async Task SetModulesAsync(Guid tenantId, IEnumerable<CrmModule> enabledModules)
    {
        var enabled = enabledModules.ToHashSet();
        var allModules = Enum.GetValues<CrmModule>();

        var existing = await _db.TenantModuleAccess
            .Where(m => m.TenantId == tenantId)
            .ToListAsync();

        foreach (var module in allModules)
        {
            var row = existing.FirstOrDefault(r => r.Module == module);
            var shouldBeEnabled = enabled.Contains(module);

            if (row == null)
            {
                _db.TenantModuleAccess.Add(new TenantModuleAccess
                {
                    TenantId  = tenantId,
                    Module    = module,
                    IsEnabled = shouldBeEnabled,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                row.IsEnabled = shouldBeEnabled;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }
}
