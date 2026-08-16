# TelecallingCRM — Project Overview

## What Is This?
A **multi-tenant, SaaS Telecalling CRM** built with ASP.NET Core 8 Razor Pages + Minimal API.  
It is designed for outbound/inbound telecalling teams (BPO, insurance, real estate, EdTech, etc.)  
and is deployed on **Railway** with a **MySQL 8** database.

## Tech Stack
| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 (Razor Pages UI + Minimal API backend) |
| ORM | Entity Framework Core 8 (Pomelo MySQL provider) |
| Auth | ASP.NET Core Identity + JWT (dual: cookie for UI, JWT for API) |
| Background Jobs | Hangfire (MySQL storage, 4 workers) |
| Real-time | SignalR (`/hubs/crm`) |
| Logging | Serilog ? console + rolling file (`logs/crm-.log`) |
| AI | OpenRouter (LLM), Whisper (transcription) |
| Messaging | WhatsApp (Meta API), SMS, Email (Resend / Mailjet via HTTPS) |
| Voice | Exotel |
| Payments | Razorpay |
| Storage | AWS S3 |
| Caching | Output Cache (ASP.NET Core built-in) + MemoryCache |
| Rate Limiting | ASP.NET Core built-in rate limiter |
| PDF | Custom `PdfService` |
| Testing | xUnit (`TelecallingCRM.Tests` project) |

## Projects
```
TelecallingCRM/              ? Main web application
TelecallingCRM.Tests/        ? Unit / integration tests
```

## Entry Point
`Program.cs` — wires up all services, middleware, and maps every API endpoint group.

## Deployment
- **Platform:** Railway (`PORT` env var ? `http://0.0.0.0:{PORT}`)
- **DB:** MySQL 8 on Railway
- **Migrations:** Auto-applied on startup via `db.Database.Migrate()`
- **Seed:** `DatabaseSeeder.SeedAsync()` runs after migrations (idempotent)
