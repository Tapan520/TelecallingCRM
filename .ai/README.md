# AI Context Files — Index

This `.ai/` folder contains project knowledge files for AI assistants (GitHub Copilot, Cursor, etc.).
Read these before working on the codebase to understand context without reading every file.

| File | What It Covers |
|---|---|
| `01-project-overview.md` | What the app is, tech stack, projects, deployment |
| `02-architecture.md` | Multi-tenancy, auth, API patterns, middleware order, caching |
| `03-domain-model.md` | All entities, enums, relationships, business rules |
| `04-conventions-and-dev-guide.md` | Folder structure, naming, how to add features, demo credentials |
| `05-services-reference.md` | All services, their interfaces, and what they do |
| `06-decisions-and-gotchas.md` | Key decisions, Railway quirks, MySQL quirks, common mistakes |

## ?? Non-Negotiable Rule
> **NEVER delete, truncate, or drop any data or table in the Railway (production) environment.**  
> This applies even if the user does not mention it. Always use soft-delete (flags) instead of physical deletion.

## Quick Facts
- **Stack**: ASP.NET Core 8, Razor Pages, Minimal API, EF Core, MySQL, Hangfire, SignalR, Serilog
- **Multi-tenant**: Every entity has `TenantId`. SuperAdmin has `TenantId = null`.
- **Auth**: Cookie (UI) + JWT (API). Roles: `superadmin`, `admin`, `manager`, `agent`.
- **Deployed on**: Railway (MySQL, auto-migrate on startup, PORT env var)
- **AI**: OpenRouter (LLM) + Whisper (transcription) per tenant
