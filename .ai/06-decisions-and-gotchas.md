# Important Decisions & Known Gotchas

---

## ?? ABSOLUTE RULE — NEVER DELETE PRODUCTION DATA

> **This rule is non-negotiable and must never be forgotten, even if the user does not mention it.**

- **NEVER** run `DELETE`, `TRUNCATE`, `DROP TABLE`, or any destructive SQL/EF operation against the **Railway (production) database**.
- **NEVER** write a migration that drops a column, drops a table, or removes data rows in production.
- **NEVER** call `db.Remove()`, `db.RemoveRange()`, `ExecuteDeleteAsync()`, or equivalent on production data.
- **NEVER** reset or re-seed production data (no `DatabaseSeeder` calls that clear existing records).
- If data cleanup is genuinely needed: **soft-delete only** — add an `IsDeleted` / `IsActive = false` flag and filter it out in queries. Never physically remove the row.
- If a migration needs to remove a column: **discuss with the developer first**. Prefer making the column nullable instead of dropping it.
- When in doubt: **do nothing destructive**. Ask the developer to confirm before any removal.

**Safe alternatives to deletion:**
| Destructive (? Never in Prod) | Safe Alternative (? Always) |
|---|---|
| `DELETE FROM Leads WHERE ...` | Set `IsActive = false` or `Status = Dead` |
| `DROP COLUMN` migration | Make column `nullable`, stop using it |
| `TRUNCATE TABLE` | Never do this in production |
| Re-running seed to overwrite | Seed is idempotent — only inserts missing records |
| `db.RemoveRange(records)` | Set a soft-delete flag, `SaveChanges()` |

---


## Architectural Decisions

### 1. Dual API Strategy (Razor Pages + Minimal API)
- The UI uses **Razor Pages** (cookie auth). All pages are in `Pages/`.
- The API uses **Minimal API** (JWT auth). All endpoints are in `Api/`.
- This allows the same app to serve a full web UI AND a public REST API.

### 2. Railway / Cloud Deployment Gotchas
- **PORT env var**: Railway injects `PORT`. The app explicitly binds `http://0.0.0.0:{PORT}`.
- **Ephemeral storage**: Log files in `logs/` are lost on redeploy. Don't rely on them for anything persistent.
- **No SMTP**: Railway blocks SMTP ports. Email is sent via HTTPS APIs only (Resend/Mailjet).
- **Slug resolution conflict**: The Railway deployment domain (`telecallingcrm.up.railway.app`) extracts `telecallingcrm` as a slug. This won't match any tenant, so the middleware correctly falls through to claim-based resolution. Do NOT try to fix this by adding a tenant with that slug.

### 3. MySQL-Specific Patterns
- Use `MySqlServerVersion(new Version(8, 0, 0))` in `UseMySql()`.
- `EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)` for transient fault handling.
- Large text fields: `HasColumnType("LONGTEXT")` (not `nvarchar(max)` as in SQL Server).
- Decimal fields: `HasColumnType("decimal(18,2)")`.

### 4. Identity Role Storage
- ASP.NET Identity roles are stored in `AspNetRoles` / `AspNetUserRoles` tables.
- Additionally, `AppUser.Role` (plain string) is used for **faster access** without joining role tables.
- **Both must stay in sync.** When creating users in code, always set `user.Role` and call `userManager.UpdateAsync(user)`.

### 5. EF Interceptor Pattern
- `AuditSaveChangesInterceptor` is registered as a **scoped** service.
- It is injected into `AppDbContext` via the service provider in `OnConfiguring`.
- Pattern: `builder.Services.AddDbContext` uses `(sp, options)` to access the DI container.

### 6. NpsSurveyResponse.TenantId
- The `TenantId` property on `NpsSurveyResponse` is **ignored by EF** (`e.Ignore(r => r.TenantId)`).
- To get the tenant, navigate through `NpsSurveyResponse.Survey.TenantId`.

### 7. JSON Serialization
- **Both** `ConfigureHttpJsonOptions` and `AddRazorPages().AddJsonOptions()` configure:
  - `JsonStringEnumConverter` (enums as strings)
  - `ReferenceHandler.IgnoreCycles` (prevent circular reference errors)
- Keep these in sync if you add serializer options.

### 8. Module Access — Default Open
- If `TenantModuleAccess` has **zero rows** for a tenant, **all modules are enabled**.
- When you add the first override row for a tenant, ALL other modules must also be explicitly set or they will be considered disabled.
- Always use `SetModulesAsync()` to set the full module configuration atomically.

### 9. Seed Data Idempotency
- The seeder checks `if (await db.Tenants.AnyAsync()) return;` for the bulk of seeding.
- **SuperAdmin** is seeded on every startup (separate idempotent check).
- **Phase 2 data** (`SeedPhase2DataAsync`) is called both on first seed and on subsequent starts (checks `if (await db.Holidays.AnyAsync()) return;`).
- If you add new seed data, add it to the appropriate idempotency block.

### 10. Rate Limiter Policies
- `"login"` ? applied to auth endpoints to prevent brute force.
- `"api"` ? applied to general API endpoints.
- Policies are implemented as `IPolicyMetadata` classes in `Services/RateLimitPolicies.cs`.

### 11. Output Cache Invalidation
- Cache is per-tenant conceptually, but keys are shared by default.
- If you add output caching to a tenant-specific endpoint, include `TenantId` in the cache key via `VaryByValue`.

### 12. SignalR Hub (`CrmHub`)
- Hub URL: `/hubs/crm`
- Used for: live dashboard data, new notification pushes, dialer state changes.
- Client connects with the cookie auth credentials (no separate JWT needed for hub).

## Common Mistakes to Avoid
- ? **DELETING or TRUNCATING any data in the Railway/production database** — use soft-delete always.
- ? **Writing DROP or destructive migrations** for production — make columns nullable instead.
- ? Querying `db.Leads` without `.Where(l => l.TenantId == tenantId)` — always scope to tenant.
- ? Running `db.Database.Migrate()` outside of startup — migrations auto-run on `Program.cs`.
- ? Sending email via SMTP — use Resend or Mailjet HTTP API.
- ? Editing `*.ide.g.cs` files — they are auto-generated by the Razor compiler.
- ? Storing secrets in `appsettings.json` — use Railway environment variables.
- ? Committing `logs/` folder contents — they are gitignored (only `logs/.gitkeep` is committed).
- ? Adding a tenant with slug matching the Railway deployment domain.
- ? Forgetting `HasColumnType("decimal(18,2)")` on money properties — EF default may not match MySQL expectations.
