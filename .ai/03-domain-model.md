# Domain Model & Business Rules

## Core Entities

### Tenant
- Represents one CRM customer (a company using the SaaS).
- Key fields: `Slug` (unique subdomain identifier), `Plan` (free/starter/pro/enterprise), `MaxUsers`, `MaxLeads`, `IsActive`.
- `PhoneMaskingEnabled`: when true, phone numbers are masked for Manager/Agent roles. Only Admin and SuperAdmin see full numbers. Settable by SuperAdmin only.
- `OpenRouterApiKey` + `PreferredModel`: per-tenant AI configuration.
- Industry options: Political, Hotel, Restaurant, RealEstate, Insurance, EdTech, Travel, Hospital, BPO, NGO, Other.

### AppUser (extends IdentityUser<Guid>)
- Roles: `superadmin`, `admin`, `manager`, `agent`
- `TenantId = null` ? SuperAdmin (platform-level, no tenant)
- `Role` is stored as a plain string column (not only in Identity's AspNetUserRoles table)

### Lead
- Core sales prospect. Has `Status`, `Priority`, `AssignedToId`, `CampaignId`.
- Lead statuses: `New`, `Contacted`, `Interested`, `FollowUp`, `Converted`, `NotInterested`, `Dead`
- DNC check: before calling, check `DncEntries` for the lead's phone (unique per tenant+phone).

### Call
- Logged after every call attempt. Has `Outcome`, `DurationSeconds`, `AiSentiment`, `AiSummary`.
- Call outcomes: `Converted`, `Interested`, `Callback`, `NotInterested`, `NoAnswer`, `Busy`, `WrongNumber`, `Voicemail`

### Campaign
- Groups leads for a calling drive. Has `Script`, `TargetCallsPerDay`, `Status` (Active/Paused/Completed).

### FollowUp
- Scheduled callback/task linked to a lead and agent. Channels: Call, WhatsApp, Email, SMS.
- Statuses: Pending, Completed, Missed.

### Deal (Pipeline)
- Tracks opportunity value through stages: Prospecting ? Qualification ? Proposal ? Negotiation ? ClosedWon / ClosedLost.
- Has `Value` (decimal), `Currency`, `Probability` (%), `ExpectedCloseDate`.

### Quote
- Quotation with line items (stored as JSON: `LineItemsJson`).
- Statuses: Draft, Sent, Accepted, Rejected, Expired.
- Fields: `SubTotal`, `DiscountAmount`, `TaxPercent`, `TaxAmount`, `Total`.

### Invoice
- Similar financial structure to Quote but for billing.
- Statuses: Draft, Sent, Paid, Overdue, Cancelled.
- Unique `InvoiceNumber` per tenant.

### Payment
- Tracks money received. Supports Razorpay (`RazorpayOrderId`, `RazorpayPaymentId`).
- Statuses: Pending, Captured, Failed, Refunded.

### Commission
- `CommissionRule`: defines type (PercentOfPayment / FlatPerConversion) and value.
- `CommissionEntry`: actual commission earned per agent per deal.
- Statuses: Pending, Approved, Paid, Rejected.

### DripSequence / DripStep / DripEnrollment
- Marketing automation. Sequences have steps (email/SMS/WhatsApp/wait/tag/assign/status-change).
- `DripEnrollment` tracks which step each lead is on (`CurrentStep`, `NextRunAt`).
- Triggers: LeadCreated, CampaignEnrolled, LeadStatusChanged, ManualEnroll.

### DispositionForm / DispositionField / DispositionResponse
- Post-call forms agents fill out. Field types: Text, Select, Checkbox, Date, Number, Rating.
- Responses stored as `AnswersJson` (Dictionary<string, string>).

### NpsSurvey / NpsSurveyResponse
- Net Promoter Score surveys triggered AfterCall or AfterConversion.
- Score 0-10. `NpsSurveyResponse.TenantId` is ignored in DB (use survey's tenant).

### AttendanceLog
- Punch in/out tracking. Agents punch in, supervisors can also punch in/out on behalf.

### LeaveRequest / LeaveBalance
- Leave management. Balance tracked per agent per year.

### AgentGoal
- Monthly targets: calls, conversions, talk time (seconds), follow-ups.

### AgentBadge (Gamification)
- Badge types: FirstCall, FirstSale, HundredCalls, LeadConvertor, FastResponder, TopPerformerWeek, TopPerformerMonth, PerfectAttendance.
- Each badge has `Points`.

### ShiftSwapRequest
- Agents can request to swap shifts. Can target a specific `SwapWithAgentId` or leave open.
- Statuses: Pending, Approved, Rejected.

### KnowledgeChunk
- AI knowledge base for the AI Assistant. Has `EmbeddingJson` (LONGTEXT) for vector search.

### CallQualityScore
- Scored by managers. Ratings: Excellent, Good, Average, BelowAverage, Poor.
- Sub-scores: Communication, ProductKnowledge, ProblemSolving, Professionalism (each 1-5).

### Expense
- Agent expense claims. Categories: Travel, Food, Internet, Equipment, Training, Other.
- Statuses: Pending, Approved, Rejected, Reimbursed.

### Announcement
- Tenant-wide notices. Priorities: Normal, Important, Urgent.
- `AnnouncementRead` tracks which users have read each announcement.

### CrmSyncConfig / CrmSyncLog
- HubSpot / Salesforce sync configuration per tenant.
- Unique per tenant+provider.

### ApiKey
- Tenant API keys with `KeyHash` (SHA256), `Scopes`, `KeyPrefix`.

### WebhookConfig / WebhookDeliveryLog
- Outbound webhooks to external URLs. `Events` stored as JSON array string.
- Signed with `Secret` via HMAC.

### RoundRobinState
- Tracks agent queue for round-robin lead assignment per campaign. `AgentQueueJson` (LONGTEXT).
- Unique per tenant+campaign.

## Important Business Rules

> ?? **PRODUCTION DATA IS SACRED — NEVER DELETE, TRUNCATE, OR DROP any data or table in the Railway (production) environment. Use soft-delete (IsActive/IsDeleted flags) always. This rule applies even if the user does not mention it.**

1. **All data is tenant-scoped.** Never query without a `TenantId` filter (except SuperAdmin).
2. **DNC check is mandatory** before initiating any outbound call or message.
3. **Lead assignment** uses round-robin by default (`LeadAssignmentService`).
4. **Phone masking**: when `Tenant.PhoneMaskingEnabled = true`, mask phones for roles `manager` and `agent`.
5. **SuperAdmin** (`TenantId = null`) can manage all tenants, see all data, enable/disable modules.
6. **Module default**: if no `TenantModuleAccess` rows exist for a tenant, all modules are ON.
7. **Migrations run on startup** automatically. Never run `dotnet ef database update` manually in production.
8. **Seed data is idempotent**: checks `if (await db.Tenants.AnyAsync())` before seeding tenants.
9. **Demo SuperAdmin**: `superadmin@telecallingcrm.com` / `SuperAdmin@12345` (seeded on every startup if missing).
10. **Razorpay webhooks** are verified by signature before processing payments.
11. **Email is sent via HTTPS** (Resend or Mailjet API) — no SMTP ports, safe for Railway.
