# Casewell — Architecture

## Context (C4 L1)

External actors and Casewell as a single box.

Diagram: [`docs/diagrams/c1-context.puml`](docs/diagrams/c1-context.puml)

Actors:
- **Attorney / Paralegal / Firm Admin / Bookkeeper** — staff users, authenticated via the firm's Azure AD B2C tenant.
- **Client** — read-only matter view, invoice payment, document signing (same B2C tenant, different role claim).
- **DocuSign** — e-signature service. Outbound for envelope creation; inbound callbacks for status changes.
- **QuickBooks Online** — accounting sync. Outbound for invoice/payment/expense posting; inbound callbacks.
- **Slack** — chatbot fanout; outbound messages + inbound webhooks.
- **Microsoft Graph (Outlook)** — email-to-matter filing; calendar sync.
- **Dropbox** — document storage replication (read-through to matter file store).
- **Anthropic Claude API** — AI assistant via private endpoint with no-retention contract.
- **Stripe** *(via QuickBooks)* — invoice payment processing.

## Containers (C4 L2)

Aspire-orchestrated runtime + external dependencies.

Diagram: [`docs/diagrams/c2-containers.puml`](docs/diagrams/c2-containers.puml)

| Container | Tech | Purpose |
|---|---|---|
| `Casewell.AppHost` | .NET 10 Aspire AppHost | Local orchestration: spins up the API, the SPA dev server, Postgres, Redis, Mailpit (dev), pgvector init |
| `Casewell.Api` | .NET 10 minimal APIs | All HTTP traffic enters here. Versioned `/api/v1/...`. Problem Details on errors. Idempotency keys on writes |
| `Casewell.Web` | Vite + React + TS + shadcn/ui + Tailwind + PWA (vite-plugin-pwa) | Dashboard SPA + Client Portal (single codebase, role-gated routes) |
| `Postgres` | Postgres 16 + pgvector extension | Operational data + audit (separate schema) + vector embeddings |
| `Redis` | Redis 7 | Distributed cache, idempotency replay store, per-tenant rate-limit windows, Hangfire job-state (optional, can run Hangfire on Postgres too) |
| `Hangfire` | in-process via `Hangfire.PostgreSql` | Scheduled jobs + reactive jobs. Dashboard at `/hangfire` (firm-admin only) |
| `Azure Key Vault` | Managed identity | All secrets (connector tokens, DB credentials in non-managed-identity scenarios, Claude API key) |
| `Azure Blob Storage` | private endpoint | Document storage. Per-matter SAS tokens (5-min expiry) for read/write |
| `OpenTelemetry Collector` | Aspire dashboard locally; Azure Monitor in production | Traces, metrics, logs |

## Components (C4 L3) — key containers

### Inside `Casewell.Api`

Diagram: [`docs/diagrams/c3-components-api.puml`](docs/diagrams/c3-components-api.puml)

- **Endpoint groups** — one per bounded context: `/api/v1/matters`, `/api/v1/documents`, `/api/v1/calendar`, `/api/v1/billing`, `/api/v1/trust`, `/api/v1/portal`, `/api/v1/ai`, `/api/v1/connectors`, `/api/v1/reports`. Each group registers via `MapMattersEndpoints(this IEndpointRouteBuilder)`.
- **Middleware pipeline (in order)**:
  1. ExceptionHandler → Problem Details (RFC 7807)
  2. Authentication (JWT Bearer via Entra ID B2C)
  3. Multi-tenancy resolver (`TenantId` from token claim → `ITenantContext`)
  4. Authorization (policy-based; policies named `<Module>.<Action>`)
  5. Idempotency check (Redis-backed, 24h replay window, only for `POST/PUT/PATCH/DELETE`)
  6. Rate limiter (per-tenant 100 req/min default; per-endpoint overrides)
  7. Audit-log writer (after successful response)
- **Use-case handlers** (MediatR-style, but minimal-API direct calls — no MediatR library; one handler class per use case to keep things simple).
- **Background-job triggers** — endpoints don't run long work; they enqueue Hangfire jobs.

### Inside `Casewell.Application.AiAssistant`

- **`MatterAssistantAgent`** — MAF `AIAgent` constructed with `MatterAssistantTool` (read matter file by id), `DocumentSearchTool` (semantic search via pgvector), `BillingSummaryTool` (read-only matter financial summary). System prompt scoped per matter.
- **`IAiAssistantService`** — abstraction over MAF. Single impl (`ClaudeMatterAssistantService`) for v1; swap-ready for Azure OpenAI / on-prem.
- **`ConversationStore`** — per-matter conversation persistence in Postgres; one row per turn with `MatterId`, `UserId`, `OccurredAt`, `Role`, `Content`, `ToolCallsJson`.

### Inside `Casewell.Web`

- **Route shell** — `<App>` with `<TenantProvider>` + `<AuthProvider>` + `<TooltipProvider>` + `<Toaster>`.
- **Layout** — sidebar nav (per role) + topbar (tenant switch + user menu + persistent global timer + chatbot toggle) + main content + slide-over `<ChatPanel>`.
- **Routes** — `/matters`, `/matters/:id` (tabbed), `/calendar`, `/billing`, `/trust`, `/reports`, `/integrations`, `/settings`. Client-role users land on `/portal/matters` instead.
- **Form stack** — shadcn `<Form>` + `react-hook-form` + `zod` per `GUARDRAILS.md` §5.
- **Table stack** — TanStack Table wrapped in `<DataTable>` shared component.

## Solution layout

```
Casewell/
├── Casewell.sln
├── global.json                          # Pins .NET 10 SDK
├── Directory.Build.props                # Shared MSBuild props (nullable, implicit usings, warnings-as-errors in Release)
├── .editorconfig
├── src/
│   ├── Casewell.AppHost/               # Aspire AppHost
│   ├── Casewell.ServiceDefaults/       # OTel + health + resilience defaults
│   ├── Casewell.Api/                   # Minimal APIs
│   ├── Casewell.Application/           # Shared application services + abstractions
│   ├── Casewell.Application.Matters/   # Matters bounded context
│   ├── Casewell.Application.Documents/ # Documents bounded context
│   ├── Casewell.Application.Calendar/  # Calendar bounded context
│   ├── Casewell.Application.Billing/   # Billing bounded context
│   ├── Casewell.Application.Trust/     # Trust bounded context
│   ├── Casewell.Application.Portal/    # Client portal bounded context
│   ├── Casewell.Application.AiAssistant/  # MAF-based matter chatbot
│   ├── Casewell.Application.Connectors/   # Connector registry + IChannel/IIntegration contracts
│   ├── Casewell.Application.Reporting/    # Read-models for dashboards
│   ├── Casewell.Domain/                # Entities + value objects + domain events + [Pii] attribute
│   ├── Casewell.Infrastructure/        # EF Core + outbox + multi-tenant filters + audit log
│   ├── Casewell.Infrastructure.Azure/  # Azure-specific impls (Blob, Key Vault, Entra B2C, Service Bus future)
│   └── Casewell.Infrastructure.Slack/  # Slack IChannel impl (working v1 connector)
├── web/
│   └── casewell-web/                   # Vite + React + TS + shadcn + Tailwind + PWA
├── infra/
│   └── azure/                           # Terraform: rg, vnet, postgres, redis, blob, key-vault, container-apps, B2C
├── tests/
│   ├── Casewell.Domain.Tests/
│   ├── Casewell.Application.Tests/
│   ├── Casewell.Api.IntegrationTests/  # WebApplicationFactory + Testcontainers (real Postgres + Redis)
│   └── Casewell.Web.Tests/             # Vitest + React Testing Library
└── docs/
    ├── diagrams/                         # C4 PlantUML
    └── architecture/                     # ADR backlog notes
```

## Cross-cutting wiring

| Concern | Implementation |
|---|---|
| **AuthN** | Azure AD B2C, single tenant for staff + clients. Custom claims: `tenant_id`, `role`. JWT Bearer in API. |
| **AuthZ** | ASP.NET Core authorization policies. Policy names: `<Module>.<Action>` (e.g. `Matters.Open`, `Trust.Post`). Bindings in `appsettings.json` under `Rbac:RoleAssignments`. |
| **Multi-tenancy** | `ITenantContext` resolved from JWT claim per request. EF Core global query filter on every entity inheriting `ITenantedEntity`. `IgnoreTenantFilter()` is audit-logged when used. |
| **Observability** | OpenTelemetry via Aspire `ServiceDefaults`. OTLP exporter → Azure Monitor in prod, Aspire dashboard locally. Traces include `tenant_id`, `matter_id`, `user_id` baggage. |
| **Health checks** | `/health`, `/health/ready`. Includes DB connectivity, Redis, Hangfire dashboard reachable. |
| **Audit logging** | Append-only `audit.audit_entries` table (separate schema). Captures `actor + action + resource + before/after JSON + idempotency_key`. PII fields redacted in `BeforeJson`/`AfterJson` per `[Pii]` attribute. |
| **Resilience** | Polly via `ServiceDefaults`. Retry (exponential backoff, max 3) + circuit breaker + 10s timeout on every outbound HTTP. |
| **Caching** | Redis. Used for: session-light state, idempotency replay records, per-tenant rate-limit windows. Per-tenant key prefix `t:{tenant_id}:...`. |
| **Background work** | **Hangfire** with `Hangfire.PostgreSql` storage. Dashboard at `/hangfire` (firm-admin only). Jobs tenant-aware (`TenantId` baked into job state). See [ADR-0001](DECISIONS.md). |
| **Outbox** | Postgres-backed outbox table per tenant. Hangfire job (`OutboxDispatcherJob`) runs every 30 seconds, takes a row lock per row, dispatches and marks processed. See [ADR-0002](DECISIONS.md). |
| **Idempotency** | `Idempotency-Key` header on all `POST/PUT/PATCH/DELETE`. 24h replay window in Redis. Replay returns the original response. |
| **Rate limiting** | ASP.NET Core `RateLimiter`. Default per-tenant 100/min; per-endpoint overrides via attributes. |
| **Problem Details** | All errors → RFC 7807. Custom `IProblemDetailsService` adds `tenantId`, `correlationId`, `traceId` to every response. |
| **Configuration** | `IOptions<T>` with `ValidateOnStart` + DataAnnotations validators. Secrets via Key Vault (managed identity in prod, environment variables in dev). |

## Cloud topology

- **Provider**: Azure (single region: `eastus2`). Future regional documented in OPERATIONS.md.
- **Compute**: Azure Container Apps for `Api`, `Web` (static-files-only via NGINX container or Azure Static Web Apps separately), Hangfire dashboard. Aspire-generated deployment manifest is the source of truth.
- **Data**: Azure Database for PostgreSQL Flexible Server (Standard tier for v1; HA-zone-redundant for prod). pgvector extension enabled.
- **Cache**: Azure Cache for Redis (Standard C0 for v1).
- **Vector store**: pgvector (same Postgres instance). Behind `IVectorStore`. See [ADR-0004](DECISIONS.md).
- **Document storage**: Azure Blob Storage with private endpoint. Per-matter SAS tokens, 5-minute expiry. See [ADR-0005](DECISIONS.md).
- **Secrets**: Azure Key Vault, managed identity for container-app → Key Vault auth.
- **Identity**: Azure AD B2C, single tenant. See [ADR-0003](DECISIONS.md).
- **Email**: Azure Communication Services (transactional). Connector `outlook` covers Graph API integration; transactional is separate.
- **Observability**: Azure Monitor + Application Insights for traces/metrics/logs.
- **Networking**: VNet with private endpoints for Postgres, Redis, Key Vault, Blob. Container Apps integrated with VNet.
- **CI/CD**: GitHub Actions. Build + test + deploy-staging on PR merge; deploy-prod via manual approval on a release.

## Data model (concrete)

EF Core entities; one-to-many relationships indicated by `→`. PII fields marked.

```csharp
// Common base
public interface ITenantedEntity { Guid TenantId { get; set; } }
public interface IAuditedEntity { DateTimeOffset CreatedAt { get; } DateTimeOffset? ModifiedAt { get; } Guid CreatedByUserId { get; } }

[AttributeUsage(AttributeTargets.Property)] public sealed class PiiAttribute : Attribute { }

// Tenancy
public record Tenant(Guid Id, string Name, string PrimaryState, string IoltaConfigJson, DateTimeOffset CreatedAt);
public record User(Guid Id, Guid TenantId, [Pii] string Email, [Pii] string FullName, string Role, string IdentityProviderSub) : ITenantedEntity;

// Matters
public record Contact(Guid Id, Guid TenantId, string Type, [Pii] string DisplayName, [Pii] string EmailJson, [Pii] string PhoneJson, [Pii] string AddressJson) : ITenantedEntity;
public record Matter(Guid Id, Guid TenantId, string MatterNumber, string Title, string PracticeArea, string Status, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, Guid PrimaryAttorneyId, Guid? ConflictCheckAttestationId) : ITenantedEntity;
public record Party(Guid Id, Guid TenantId, Guid MatterId, Guid ContactId, string RoleInMatter) : ITenantedEntity;
public record ConflictAttestation(Guid Id, Guid TenantId, Guid MatterId, Guid AttestedByUserId, DateTimeOffset PerformedAt, string AttestationHash, string? PriorAttestationHash, string DataSnapshotJson) : ITenantedEntity;

// Documents
public record Document(Guid Id, Guid TenantId, Guid MatterId, string Filename, string StorageUri, int Version, DateTimeOffset UploadedAt, Guid UploadedByUserId, bool ContainsPii) : ITenantedEntity;
public record DocumentTemplate(Guid Id, Guid TenantId, string Name, string WordTemplateUri, string MergeFieldsJson) : ITenantedEntity;

// Calendar
public record CalendarEvent(Guid Id, Guid TenantId, Guid MatterId, string Type, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Title, string Description, string ReminderRuleJson) : ITenantedEntity;

// Billing
public record TimeEntry(Guid Id, Guid TenantId, Guid MatterId, Guid UserId, int DurationMinutes, string ActivityCode, string Narrative, bool Billable, Guid? BilledInvoiceId) : ITenantedEntity;
public record Expense(Guid Id, Guid TenantId, Guid MatterId, decimal Amount, string Description, DateOnly IncurredOn, bool Billable, Guid? BilledInvoiceId) : ITenantedEntity;
public record Invoice(Guid Id, Guid TenantId, Guid MatterId, string Status, DateTimeOffset GeneratedAt, DateTimeOffset? ApprovedAt, DateTimeOffset? SentAt, DateTimeOffset? PaidAt, decimal TotalAmount, Guid? ApprovedByUserId) : ITenantedEntity;

// Trust
public record TrustAccount(Guid Id, Guid TenantId, string Jurisdiction, string AccountNumberLast4, decimal CurrentBalance) : ITenantedEntity;
public record TrustLedgerEntry(Guid Id, Guid TenantId, Guid TrustAccountId, Guid MatterId, Guid ContactId, DateTimeOffset PostedAt, decimal Amount, string EntryType, string Description, Guid? ReconciliationId) : ITenantedEntity;
public record Reconciliation(Guid Id, Guid TenantId, Guid TrustAccountId, string Period, decimal BankBalance, decimal BookBalance, string Status, DateTimeOffset? ReconciledAt, Guid? ReconciledByUserId) : ITenantedEntity;

// Connectors
public record InstalledConnector(Guid Id, Guid TenantId, string Name, bool Enabled, string ConfigSecretRefJson, DateTimeOffset InstalledAt) : ITenantedEntity;

// Cross-cutting (separate `audit` schema for AuditEntry)
public record OutboxMessage(Guid Id, Guid TenantId, DateTimeOffset OccurredAt, string AggregateType, Guid AggregateId, string EventType, string PayloadJson, DateTimeOffset? ProcessedAt, int Attempts) : ITenantedEntity;
public record AuditEntry(Guid Id, Guid TenantId, DateTimeOffset OccurredAt, Guid ActorUserId, string ActorRole, string Action, string ResourceType, Guid ResourceId, string BeforeJson, string AfterJson, string? IdempotencyKey) : ITenantedEntity;
```

Migrations: `dotnet ef migrations add <name> --project src/Casewell.Infrastructure`. Migration files checked into git. Production deploy runs migrations as a separate Container App job, never inline at app startup.

Trust-account invariants enforced as DB constraints (`amount > 0` for deposits, `amount < 0` for disbursements, balance computed as a derived column) AND as `Domain` invariants checked before the EF write. Hard guardrails per [SPEC.md] answer #11.

## API surface (concrete)

```
GET    /api/v1/matters                          # list (paginated)
POST   /api/v1/matters                          # open new matter (requires ConflictAttestation in body)
GET    /api/v1/matters/{id}                     # detail
PATCH  /api/v1/matters/{id}                     # update
POST   /api/v1/matters/{id}/close

POST   /api/v1/matters/{id}/conflict-check      # perform attestation; returns hash
GET    /api/v1/matters/{id}/conflict-attestations

GET    /api/v1/matters/{id}/documents
POST   /api/v1/matters/{id}/documents           # multipart; returns SAS token for direct upload
POST   /api/v1/matters/{id}/documents/from-template  # merge a template

GET    /api/v1/matters/{id}/time
POST   /api/v1/matters/{id}/time
POST   /api/v1/matters/{id}/time/timer/start
POST   /api/v1/matters/{id}/time/timer/stop

GET    /api/v1/matters/{id}/invoices
POST   /api/v1/matters/{id}/invoices            # draft
POST   /api/v1/invoices/{id}/approve
POST   /api/v1/invoices/{id}/send

GET    /api/v1/trust/accounts
GET    /api/v1/trust/accounts/{id}/ledger
POST   /api/v1/trust/accounts/{id}/ledger       # post entry; API-layer guardrails apply
POST   /api/v1/trust/accounts/{id}/reconcile

GET    /api/v1/calendar                         # query by date range
POST   /api/v1/matters/{id}/calendar
PATCH  /api/v1/calendar/{id}

GET    /api/v1/portal/matters                   # client-scope; only matters where the client is a party
POST   /api/v1/portal/invoices/{id}/pay
POST   /api/v1/portal/documents/{id}/sign

POST   /api/v1/ai/matters/{id}/ask              # AI matter assistant; streams via SSE
GET    /api/v1/ai/matters/{id}/conversation

GET    /api/v1/connectors                       # list installed
POST   /api/v1/connectors/{name}/install        # admin-only
POST   /api/v1/connectors/{name}/webhook        # per-connector inbound

GET    /api/v1/reports/origination
GET    /api/v1/reports/realization
GET    /api/v1/reports/wip
GET    /api/v1/reports/ar-aging
GET    /api/v1/reports/trust-reconciliation-status

# GDPR / compliance
POST   /api/v1/tenants/{id}/export              # admin-only; enqueues export job
POST   /api/v1/tenants/{id}/data-subject-request

# Health
GET    /health
GET    /health/ready
```

Every write endpoint accepts `Idempotency-Key`. All endpoints versioned via URL segment. Errors → Problem Details.

## MAF agents

### `MatterAssistantAgent`

- **Purpose**: answer attorney/paralegal questions about a specific matter.
- **Construction**: `AIAgent` over `IChatClient` (Claude), with `MatterContextTool` (returns matter overview), `DocumentSearchTool` (semantic search via `IVectorStore`), `BillingSummaryTool` (returns time/invoice summary), `CalendarSummaryTool`.
- **System prompt outline**: "You are a legal practice assistant for matter {matter_number} at {firm_name}. You have read-only access to the matter file. You answer questions about the matter and draft summaries. You DO NOT give legal advice. Attorney-client privilege applies; do not retain or repeat information outside this conversation."
- **Memory**: per-matter conversation persisted in `ConversationStore` (Postgres). Last 20 turns provided as context window.
- **Tool filter**: `BillingSummaryTool` requires `Time.View` policy; if user lacks it, the tool is omitted from the agent's tool list for this session.

## SPA architecture

- **Bundler**: Vite. PWA via `vite-plugin-pwa` (offline manifest + service worker).
- **State**: TanStack Query for server state; Zustand for ephemeral UI state. No Redux.
- **Routing**: React Router v6 with lazy-loaded route segments per bounded context.
- **Components**: shadcn primitives (Button, Form, Dialog, Sheet, Table, Toast, etc.), copied into `web/casewell-web/src/components/ui/` per shadcn convention (we own them).
- **Forms**: shadcn `<Form>` + `react-hook-form` + `zod` validation. One zod schema per request body, shared with the API via `valibot-zod` codegen *(planned; v1 hand-aligned)*.
- **Tables**: TanStack Table inside a shared `<DataTable>` component.
- **Chat panel**: `<ChatPanel>` slide-over via shadcn `<Sheet>`. Streams responses via SSE from `/api/v1/ai/matters/{id}/ask`.
- **Persistent timer**: top-bar `<MatterTimer>` component, scoped to the focused matter from the global store.
- **Theme**: Tailwind + shadcn theming; light/dark toggle.

## Diagrams checked into the repo

- [`docs/diagrams/c1-context.puml`](docs/diagrams/c1-context.puml) — actors + system box
- [`docs/diagrams/c2-containers.puml`](docs/diagrams/c2-containers.puml) — Aspire-orchestrated containers + Azure dependencies
- [`docs/diagrams/c3-components-api.puml`](docs/diagrams/c3-components-api.puml) — middleware pipeline + endpoint groups inside `Casewell.Api`
