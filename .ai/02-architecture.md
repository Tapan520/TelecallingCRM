# Architecture & Key Patterns

## 1. Multi-Tenancy
- Every entity has a `TenantId` (Guid) foreign key to the `Tenants` table.
- **`TenantMiddleware`** resolves the current tenant on every request using this priority:
  1. Slug from subdomain / `X-Tenant-Slug` header / `?tenant=` query param
  2. `tenant_id` claim in the auth cookie / JWT
  3. DB lookup by `UserId` claim (fallback for stale cookies)
- **`TenantContext`** (scoped service) holds the resolved `Tenant` for the lifetime of a request.
- `SuperAdmin` users have `TenantId = null` and bypass tenant resolution.

## 2. Module Access Control
- Each tenant can have specific CRM modules enabled/disabled via `TenantModuleAccess` table.
- **`TenantModuleService`** returns `HashSet<CrmModule>` of enabled modules per tenant.
- If **no rows exist** for a tenant ? **all modules are enabled** (default-open).
- `ModuleAccessFilter` (MVC global filter) enforces module access on Razor Pages.
- Enabled modules are stored in `HttpContext.Items["EnabledModules"]` per request.

## 3. Dual Authentication
- **Cookie auth** ? Razor Pages (UI). Login path: `/Login`.
- **JWT Bearer** ? Minimal API (`/api/*` routes). Token issued by `TokenService`.
- For `/api/*` requests that hit the cookie auth redirect: middleware returns `401`/`403` directly instead of redirecting to HTML pages.
- `AppUserClaimsPrincipalFactory` enriches claims with `tenant_id`, `role`, `full_name`.

## 4. API Layer (Minimal API)
- All endpoints live in `Api/` folder as static extension classes (`Map*Endpoints()`).
- Each file registers a group of related routes (e.g., `LeadsEndpoints`, `CallsEndpoints`).
- All registered in `Program.cs` via `app.Map*Endpoints()`.
- Swagger UI available at `/swagger` in Development.

## 5. Razor Pages (UI Layer)
- Pages live in `Pages/` folder, organized by feature area.
- Each page folder has a `.cshtml` (view) and `.cshtml.cs` (PageModel).
- Pages use cookie auth and call the Minimal API internally or use EF directly.

## 6. Background Jobs (Hangfire)
- Dashboard at `/hangfire` (admin-only, protected by `HangfireAuthFilter`).
- Recurring jobs registered in `ScheduledJobService.RegisterRecurringJobs()`.
- Workers: 4 concurrent.

## 7. Real-time (SignalR)
- Hub: `CrmHub` at `/hubs/crm`.
- Used for live dashboard updates, notifications, dialer events.

## 8. Audit Trail
- `AuditSaveChangesInterceptor` automatically records changes to `ActivityLog`.
- Interceptor registered as scoped service and injected into `AppDbContext`.

## 9. Data Layer
- `AppDbContext` extends `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`.
- All relationships and indexes are configured in `OnModelCreating`.
- Composite indexes are added for common query patterns (tenant+status, agent+date, etc.).
- Large text fields use `HasColumnType("LONGTEXT")` (MySQL-specific).
- Money fields use `HasColumnType("decimal(18,2)")`.

## 10. Middleware Order (important)
```
UseStaticFiles          ? before tenant middleware (no DB hit on assets)
UseRouting
UseRateLimiter
UseOutputCache
UseAuthentication
UseMiddleware<TenantMiddleware>
UseAuthorization
```

## 11. Output Caching Policies
| Policy | TTL | Tag |
|---|---|---|
| `dashboard` | 60 s | `dashboard` |
| `leaderboard` | 120 s | `leaderboard` |
| `reports` | 300 s | `reports` |
| default | no cache | — |

## 12. Rate Limiting
- `login` policy ? `LoginRateLimitPolicy`
- `api` policy ? `ApiRateLimitPolicy`
- Rejection status: `429 Too Many Requests`
