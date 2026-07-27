# Module Access Control - Troubleshooting Guide

## How It Works

The TelecallingCRM uses a **3-layer module access control system**:

### 1. ? Middleware Layer
- **File**: `Services/TenantMiddleware.cs`
- **What it does**: On every request, loads the tenant's enabled modules from `TenantModuleAccess` table and stores them in `HttpContext.Items["EnabledModules"]`
- **Default behavior**: If no `TenantModuleAccess` records exist for a tenant, **ALL modules are enabled by default**

### 2. ? UI Layer
- **File**: `Pages/Shared/_Layout.cshtml`
- **What it does**: Checks `ModOn(CrmModule.X)` before rendering navigation links
- **Code example**:
```csharp
@if (ModOn(CrmModule.Escalations)) {
    <a class="nav-link" asp-page="/Escalations/Index">
        <i class="bi bi-exclamation-triangle"></i> Escalations
    </a>
}
```

### 3. ? Page Authorization Layer
- **Files**: `Services/ModuleAccessFilter.cs` + Page handlers (`.cshtml.cs`)
- **What it does**: The `[RequireModule(CrmModule.X)]` attribute on page handlers redirects users to `/AccessDenied` if they try to access a disabled module directly
- **Code example**:
```csharp
[Authorize]
[RequireModule(CrmModule.Escalations)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
```

---

## Debugging "Agent Can Still Access Escalations"

If an agent can see and click on the "Escalations" link despite the module being disabled:

### Step 1: Verify Tenant Module Configuration
Run this SQL query to check the tenant's module settings:

```sql
SELECT * FROM TenantModuleAccess 
WHERE TenantId = 'YOUR-TENANT-GUID' 
  AND Module = 11; -- 11 = Escalations
```

**Expected results**:
- **No rows**: All modules are enabled by default (opt-out model)
- **Row with `IsEnabled = 0`**: Module is disabled
- **Row with `IsEnabled = 1`**: Module is enabled

### Step 2: Check If Module Access Was Ever Configured
```sql
SELECT COUNT(*) FROM TenantModuleAccess WHERE TenantId = 'YOUR-TENANT-GUID';
```

If the count is **0**, the tenant has never had module restrictions configured. Go to **SuperAdmin ? Tenants ? Module Access** to configure.

### Step 3: Verify the Page Has the Attribute
Check that `Pages/Escalations/Index.cshtml.cs` has:
```csharp
[RequireModule(CrmModule.Escalations)]
```

### Step 4: Clear Browser Cache
The navigation is rendered server-side but the tenant context might be cached. Have the user:
1. **Hard refresh** the page (Ctrl+F5 / Cmd+Shift+R)
2. **Sign out and sign in** again
3. **Clear browser cookies** for the site

### Step 5: Check SuperAdmin Bypass
SuperAdmins are **always allowed** access to all modules regardless of tenant configuration. Verify the user's role:
```sql
SELECT Role FROM AspNetUsers WHERE Id = 'USER-GUID';
```

If `Role = 'superadmin'`, this is expected behavior.

---

## Module Enum Reference

```csharp
public enum CrmModule
{
    Leads = 1,
    LeadImport = 2,
    Pipeline = 3,
    Campaigns = 4,
    Broadcast = 5,
    Dialer = 6,
    CallLog = 7,
    LiveMonitor = 8,
    Payments = 9,
    WhatsApp = 10,
    Escalations = 11,  // ? The one mentioned in the issue
    // ... (50+ total modules)
}
```

---

## How to Disable a Module for a Tenant

1. **Login as SuperAdmin**
2. Navigate to **SuperAdmin ? Tenants**
3. Click the **Module Access** (puzzle icon) button for the tenant
4. **Uncheck** the modules you want to disable
5. Click **Save**

This will:
- Insert/update records in `TenantModuleAccess` table
- Hide the navigation links immediately (next page load)
- Block direct URL access via the `[RequireModule]` filter

---

## Common Issues

### Issue: "Module links still showing after disabling"
**Solution**: User needs to refresh the page (F5). The `EnabledModules` set is loaded per-request.

### Issue: "Agent can type URL directly and access the page"
**Solution**: This should NOT work if the page has `[RequireModule]` attribute. Verify the attribute exists in the page handler.

### Issue: "All modules enabled even though I disabled some"
**Solution**: Check if the `TenantModuleAccess` table has any rows for that tenant. If empty, all modules are enabled by default (opt-out model).

---

## Pages That Should NEVER Have Module Restrictions

The following pages should be accessible regardless of module configuration:

- ? **Dashboard** (`/Dashboard/Index`) - Core functionality
- ? **Profile** (`/Profile/Index`) - User settings
- ? **Login/Logout** (`/Login`, `/Logout`) - Authentication
- ? **Admin Users** (`/Admin/Users`) - Admin panel for role=admin/manager
- ? **Admin Settings** (`/Admin/Settings`) - Tenant configuration
- ? **SuperAdmin pages** (`/SuperAdmin/*`) - Platform management

These pages should only have `[Authorize]` or role-based authorization, **NOT** `[RequireModule]`.

---

## Need More Help?

If the issue persists:
1. Enable debug logging in `Program.cs`
2. Check the application logs for `ModuleAccessFilter` output
3. Verify the `TenantMiddleware` is correctly populating `HttpContext.Items["EnabledModules"]`
4. Open browser DevTools ? Network tab ? Check the response headers for `/api/*` calls
