# Coding Conventions & Developer Guide

## Folder Structure
```
TelecallingCRM/
??? Api/                    # Minimal API endpoint classes (one file per feature)
??? Data/
?   ??? Models/             # EF entity classes
?   ??? Migrations/         # EF migrations (auto-generated)
?   ??? AppDbContext.cs     # DbContext + OnModelCreating
?   ??? DatabaseSeeder.cs   # Demo data seed (idempotent)
?   ??? AuditSaveChangesInterceptor.cs
??? Hubs/
?   ??? CrmHub.cs           # SignalR hub
??? Pages/                  # Razor Pages (UI), organized by feature folder
??? Services/               # Business logic, middleware, background jobs
??? wwwroot/                # Static files (Bootstrap, jQuery, custom CSS/JS)
??? logs/                   # Runtime log files (gitignored, .gitkeep present)
??? GlobalUsings.cs         # Global using statements
??? Program.cs              # App entry point
```

## Naming Conventions
- **Endpoint files**: `{Feature}Endpoints.cs` in `Api/` folder. Extension method: `Map{Feature}Endpoints(app)`.
- **Services**: Interface `I{Name}Service`, implementation `{Name}Service` in `Services/`.
- **Models**: Plain class name in `Data/Models/`. No `Model` suffix.
- **Razor Pages**: `Pages/{Feature}/Index.cshtml` + `Index.cshtml.cs`.
- **Migrations**: auto-generated, do not edit manually.

## Adding a New Feature — Checklist
1. Create model(s) in `Data/Models/`
2. Add `DbSet<T>` to `AppDbContext`
3. Configure relationships + indexes in `OnModelCreating`
4. Run `dotnet ef migrations add <MigrationName>`
5. Create `Api/{Feature}Endpoints.cs` with `Map{Feature}Endpoints()`
6. Register in `Program.cs`: `app.Map{Feature}Endpoints();`
7. (Optional) Create Razor Page in `Pages/{Feature}/`
8. (Optional) Add `CrmModule` enum value and register in `TenantModuleAccess` if feature is module-gated

## Service Registration Pattern
```csharp
// Scoped (most services)
builder.Services.AddScoped<IMyService, MyService>();

// Singleton (stateless/thread-safe only)
builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
```

## Tenant Context in API Endpoints
```csharp
// Always resolve tenant in endpoint handlers:
var tenantCtx = httpContext.RequestServices.GetRequiredService<TenantContext>();
var tenantId = tenantCtx.Tenant?.Id ?? throw new UnauthorizedAccessException();

// Then filter all DB queries by tenantId:
var leads = await db.Leads.Where(l => l.TenantId == tenantId).ToListAsync();
```

## Money / Decimal Fields
- Always use `decimal` type in C#.
- Always configure in EF: `e.Property(x => x.Amount).HasColumnType("decimal(18,2)")`.

## JSON Columns
- Large JSON blobs (line items, embeddings, queue state) use `string` in C# + `HasColumnType("LONGTEXT")`.
- Serialize with `System.Text.Json.JsonSerializer`.

## Output Caching
- Use named policies: `"dashboard"`, `"leaderboard"`, `"reports"`.
- Apply to endpoint: `.CacheOutput("dashboard")`.
- Invalidate by tag using `IOutputCacheStore`.

## Error Handling
- Production: `/Error` page via `UseExceptionHandler`.
- API endpoints: return appropriate HTTP status codes (`Results.NotFound()`, `Results.BadRequest()`, etc.).

## Important: `*.ide.g.cs` Files
Files like `Pages/Shared/_Layout.cshtml.r862omQb7rwsKWgj.ide.g.cs` are **auto-generated** by the IDE Razor compiler. Do NOT edit or delete them manually.

## appsettings.json Keys (Required)
```json
{
  "ConnectionStrings": { "DefaultConnection": "..." },
  "Jwt": { "Secret": "...", "Issuer": "...", "Audience": "..." }
}
```
- `appsettings.Development.json` and `appsettings.Local.json` are **gitignored** (never commit).
- On Railway, all secrets are set as **environment variables**.

## Database Migrations
```bash
# Add migration
dotnet ef migrations add <Name> --project TelecallingCRM.csproj

# Apply (never needed in prod — auto-applied on startup)
dotnet ef database update
```

## Running Locally
```bash
dotnet run --project TelecallingCRM.csproj
# App starts at https://localhost:5001 (or http://localhost:5000)
# Swagger UI: /swagger
# Hangfire: /hangfire
# Health: /health
```

## Demo Credentials (Seeded)
| Role | Email | Password |
|---|---|---|
| SuperAdmin | superadmin@telecallingcrm.com | SuperAdmin@12345 |
| Admin (Apex) | admin@apexsales.com | Admin@12345 |
| Manager (Apex) | manager@apexsales.com | Manager@12345 |
| Agent (Apex) | alice@apexsales.com | Agent@12345 |
| Agent (Apex) | bob@apexsales.com | Agent@12345 |
| Admin (Nova) | admin@novatelecom.com | Admin@12345 |
| Agent (Nova) | raj@novatelecom.com | Agent@12345 |

## Demo Tenants
| Tenant | Slug | Plan |
|---|---|---|
| Apex Sales Co | apex-sales | pro |
| Nova Telecom | nova-telecom | starter |
