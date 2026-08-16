# Services Reference

## Core Services

### `TenantContext` (scoped)
Holds the resolved `Tenant` for the current request. Check `IsResolved` before using.

### `ITenantResolver` ? `TenantResolver`
Extracts the tenant slug from subdomain, `X-Tenant-Slug` header, or `?tenant=` query.

### `ITenantModuleService` ? `TenantModuleService`
Returns enabled `CrmModule` enum values per tenant. Default: all enabled.

### `ITokenService` ? `TokenService`
Issues JWT access tokens + refresh tokens for API authentication.

### `ILeadAssignmentService` ? `LeadAssignmentService`
Round-robin lead assignment using `RoundRobinState` per campaign.

### `IOpenRouterService` ? `OpenRouterService`
Calls OpenRouter LLM API. Uses per-tenant `OpenRouterApiKey` and `PreferredModel`.

### `IWhisperService` ? `WhisperService`
Transcribes call recordings using Whisper API.

### `IKnowledgeService` ? `KnowledgeService`
Manages AI knowledge base chunks. Handles embedding and semantic search.

### `ICallAiProcessor` ? `CallAiProcessor`
Post-call AI processing: transcription, sentiment analysis, summary generation.

### `IMessageDispatcher` ? `MessageDispatcher`
Unified dispatcher for sending WhatsApp, SMS, and Email messages.

### `IWebhookDispatcher` ? `WebhookDispatcher`
Dispatches outbound webhook events to configured tenant webhook URLs (HMAC-signed).

### `INotificationSender` ? `NotificationSender`
Creates in-app notifications and pushes real-time updates via SignalR.

### `IUserActivityLogger` ? `UserActivityLogger`
Logs user activity to `UserActivityLog` table (page visits, actions).

### `ScheduledJobService`
Registers and handles Hangfire recurring background jobs.

### `ICrmSyncService` ? `CrmSyncService`
Syncs leads/contacts to HubSpot or Salesforce.

### `IInvoiceService` ? `InvoiceService`
Invoice generation logic (number sequencing, tax calculation).

### `IPdfService` ? `PdfService`
Generates PDF files (invoices, quotes).

### `IS3StorageService` ? `S3StorageService`
Uploads/downloads files to/from AWS S3.

### `IExotelVoiceService` ? `ExotelVoiceService`
Integrates with Exotel for click-to-call and call control.

### `ILocalizationService` ? `LocalizationService` (singleton)
Provides localized string resources.

### `ModuleAccessFilter` (MVC global filter)
Applied to all Razor Pages. Checks `HttpContext.Items["EnabledModules"]` and returns 403 if a page's required module is disabled.

### `HangfireAuthFilter`
Restricts Hangfire dashboard to `admin` and `superadmin` roles.

### `PhoneNumberHelper`
Utility for normalizing and masking phone numbers.

## Named HTTP Clients
| Name | Base URL | Purpose |
|---|---|---|
| `openrouter` | — | OpenRouter LLM |
| `whisper` | — | Whisper transcription |
| `exotel` | — | Exotel voice |
| `hubspot` | — | HubSpot CRM sync |
| `salesforce` | — | Salesforce CRM sync |
| `resend` | https://api.resend.com | Email (Resend) |
| `mailjet` | https://api.mailjet.com | Email (Mailjet) |
