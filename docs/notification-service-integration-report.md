# Notification service integration implementation

Date: 2026-09-09

All implementation changes are in `mpdcapitemplate`. The OTC, Weighbridge and TAN repositories were not modified. Existing uncommitted notification refactoring was preserved.

## Implemented

| Area | Result |
|---|---|
| Application profiles | `otc-api`, `weighbridge-api`, and `tan-webservice` profiles in `Api/appsettings.json`. Each controls its sender, reply-to, allowed templates, raw-content permission, subject overrides, recipient count, attachment count and total attachment size. |
| Machine authentication | `Authorization: ApiKey <application-id>.<secret>` authenticates an application using its configured SHA-256 digest. Keys grant notification send/read only, not replay. No real keys are committed. Existing JWT authentication remains available. |
| Application isolation | Application identity comes from authentication, not the request body. Status and search are scoped to the caller; admins can inspect all applications. JWT applications require `application_id` and the appropriate `notification_permission` (`send`/`read`) claims. |
| Domain templates | Added 28 unversioned templates for OTC, Weighbridge and TAN. See [the template catalogue](domain-notification-templates.md) for variables, workflow sources and attachment requirements. |
| Rich email | Multiple To recipients, CC, raw HTML/text, or file templates. The original single `recipient` field remains usable. Raw content and templates are mutually exclusive. |
| Durable attachments | Multipart upload stores bytes in SQL/SQLite with an owning application. Email requests carry attachment IDs; Kafka carries references rather than binary data. Ownership, file name, MIME type, count and total size are checked. Uploaded data survives restarts and remains available for retries and replay. |
| Inline logos | Attachment `contentId` supports SMTP inline resources referenced as `cid:...` in HTML. Brevo inline references are rejected before acceptance because this adapter does not implement that path. |
| Providers | Added configurable SMTP delivery; extended Brevo with multiple recipients, CC and base64 attachments. Fake remains the default. |
| Idempotency | Unique `(ApplicationId, IdempotencyKey)` plus a request fingerprint. Identical retries return the original notification; changed content returns 409. Dictionary insertion order does not change the fingerprint. Existing rows without a fingerprint retain their original duplicate behavior. |
| Validation | REST and Kafka share the request validator and preflight checks. Recipient/header limits, template existence/variables, content mode, application permissions and attachment ownership are checked before queuing. Replay checks profile, template and attachments again. |
| Status/search | `GET /api/v1/notifications` filters by business reference, correlation ID or idempotency key, with bounded pagination. Status includes application/business references and explains that Sent means provider acceptance. |
| Replay | Restricted to Admin in both the endpoint and application handler. Existing replay audit records are retained. |
| Kafka | REST produces an EmailRequestedV1 contract carrying ownership and rich content. The consumer also accepts requests without application context. Application-scoped messages on the shared topic must match a durable REST submission; a publisher cannot claim another application's identity simply by supplying its ID. Duplicate consumption remains protected by the inbox. |
| Kafka-independent REST | With `Kafka:Enabled=false`, acceptance writes a queued notification directly. Delivery retries and status work without Kafka. Exhausted failures remain `PermanentlyFailed`; no unpublishable DLQ outbox is created. |
| Logging | Notification MediatR request bodies are excluded from normal, slow-request and exception log templates. |
| Template version removal | Template files now resolve by ID only; API/contracts/rendering no longer use template versions. A separate `RemoveTemplateVersion` migration drops the old column. Existing queued envelopes and idempotent retries remain supported. |
| Persistence | Generated `20260909082451_AddNotificationApplicationIntegration`: ownership/business/fingerprint/content fields, scoped indexes and attachment table. Existing notifications become `legacy`. Migration was not applied to an existing database. |

## API integration examples

Use HTTPS for application credentials. Generate a high-entropy random secret (at least 32 random bytes), give the secret to the producer, and configure only its SHA-256 hexadecimal digest in the notification service, for example:

```text
NotificationIntegration__Applications__otc-api__ApiKeySha256=<SHA256-HEX-DIGEST>
NotificationIntegration__Applications__otc-api__SenderEmail=<APPROVED-SENDER>
EmailProvider__Provider=Smtp
Smtp__Host=<MAIL-RELAY>
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__Username=<IF-REQUIRED>
Smtp__Password=<SECRET>
```

SMTP uses STARTTLS when SSL is enabled; an implicit TLS-only relay needs a different adapter. One configured provider serves all applications. Each application has its own configured sender. Caller-selected arbitrary From addresses are not supported.

For Brevo, select `EmailProvider__Provider=Brevo`, configure its API key and approved sender profiles. The adapter's To, CC and base64 attachment mapping follows the [Brevo transactional email API](https://developers.brevo.com/reference/send-transac-email). Provider-side idempotency must still be verified for the production account; no exactly-once guarantee is made.

Upload the generated report first:

```http
POST /api/v1/notifications/attachments
Authorization: ApiKey otc-api.<secret>
Content-Type: multipart/form-data; boundary=...

Form field: file (ticket.pdf, application/pdf)
```

Response: `201` with `attachmentId`, `fileName`, `contentType`, and byte `size`.

Then submit:

```http
POST /api/v1/notifications/email
Authorization: ApiKey otc-api.<secret>
Content-Type: application/json

{
  "idempotencyKey": "ticket:123:issued:v1",
  "correlationId": "receipt:123",
  "businessReference": "receipt:123",
  "to": [{ "email": "transporter@example.com", "name": "Transporter" }],
  "cc": [{ "email": "operations@example.com" }],
  "subject": "Weighbridge ticket 123",
  "htmlBody": "<p>Please find your ticket attached.</p>",
  "textBody": "Please find your ticket attached.",
  "attachments": [{ "attachmentId": "<UUID-FROM-UPLOAD>" }]
}
```

For template messages, omit `htmlBody`/`textBody` and supply `templateId` and `variables`. `payment-confirmed` requires `customerName` and `paymentReference`. Use either `recipient` or `to`, not both. Lists contain individual addresses, not comma/semicolon-separated strings.

The response is `202 Accepted` with notification ID, message ID, status and a Location header. It is not confirmation that the mail was sent. Query:

```text
GET /api/v1/notifications/{notificationId}
GET /api/v1/notifications?businessReference=receipt:123&skip=0&take=50
GET /api/v1/notifications?idempotencyKey=ticket:123:issued:v1
```

Search defaults to 50 entries and permits at most 100. Admins may also specify `applicationId`.

Default application limits: 50 To+CC recipients, 5 attachments, 5 MiB total attachment bytes. JSON requests are limited to 512 KiB and combined bodies to 100,000 characters. The upload endpoint has an 11 MiB hard request limit; profile limits are applied below it. Supported upload types are PDF, XLSX, XLS, CSV, plain text, PNG and JPEG. Files are validated by declared type and name, not scanned for malware.

## Compatibility and deployment

- `AllowLegacyJwtClients=true` preserves existing JWT callers and V1 Kafka input in the shared `legacy` application. Disable it after those callers migrate. It is not isolation between individual legacy users.
- Disallowed subject overrides now return a validation error instead of being silently discarded.
- Deploy the migration before using the new application binary. Stop the old consumer before starting the updated publisher; old consumers do not understand application ownership and rich content. Retain a single service replica.
- SQL Server uses the generated migration. Existing development SQLite databases use `EnsureCreated`, which does not upgrade an existing schema: back up and deliberately migrate/recreate a disposable development database before running the new version. This change does not delete or modify an existing database automatically.
- Do not toggle Kafka off with existing Pending notifications/outbox work unless that work has been drained or an explicit reconciliation has been performed. Direct mode applies to new submissions; it does not convert existing pending work.
- Downgrading requires checking for keys reused by different applications before restoring the old globally unique index. The migration's Down operation also removes attachment/content data.
- `Sent` means accepted by the configured provider. With Fake, it is simulated acceptance. SMTP group-recipient errors are treated as permanent because some recipients may already have received the mail; automatic group retry could duplicate it. Support must reconcile any partial delivery before replaying.

## Still missing in the notification service

- SMS delivery and Twilio integration; TAN's driver notifications are not covered yet.
- Provider webhooks, confirmed delivery/bounce states, and outbound status callbacks/events. Consumers must poll the available status API.
- Atomic delivery claims and recovery for multiple replicas; keep one worker instance. Provider acceptance followed by a crash before persistence can still produce duplicates.
- Independently authenticated, application-scoped direct Kafka publishing. New applications should use REST. A future Kafka producer path needs topic/ACL identity mapping; payload application IDs alone are insufficient.
- Translated templates, repeated-row template syntax, and dedicated variants for remaining FMS, rental and induction workflows. The 28 implemented domain templates are documented in [the catalogue](domain-notification-templates.md).
- Multiple selectable sender profiles within one application and per-application provider selection. Current profiles each provide one sender and reply-to.
- Attachment cleanup/retention automation, deduplicated uploads, external object storage, malware scanning and per-application storage quotas. Attachments currently remain in the database indefinitely, so storage growth needs an operational plan before report-heavy adoption.
- Rate limiting, delivery fairness between applications, notification-specific queue/lag metrics and provider/Kafka readiness probes. Current readiness checks the database; current worker logs include IDs, attempt outcomes and duration.
- Live SMTP/Brevo/Kafka smoke tests and SQL Server migration execution in a staging environment.

## Work required in OTC, Weighbridge and TAN

No producer changes were made. Each application still needs an adapter for this API, configured credentials, stable event-based idempotency keys, notification-ID storage and status reconciliation. Important business workflows need a producer outbox saved in the same database transaction as the business change. Report generation and recipient selection remain in those applications.

TAN must stop treating every successful HTTP response as `WasTicketSentToTransporter=true`. Track submitted versus provider-accepted separately. Its existing SMS path must remain until the service gains SMS support.

## Verification

Tests use Fake email, captured HTTP payloads and in-memory SQLite; they send no real messages. The existing SQL Server/Testcontainers tests require Docker, which was not running in this environment.

- API build completed with zero warnings/errors.
- Initial integration implementation verification: **140 passing tests** — Application 82, Domain 17, Infrastructure 26, Persistence 9, and notification API integration 6. The final targeted integration run passed after correcting JWT configuration initialization exposed by the admin replay test.
- Domain-template follow-up: **173 tests pass** across the runnable suite. See [the template catalogue](domain-notification-templates.md#verification) for coverage and deployment verification.
- EF `has-pending-model-changes` reports that the migration matches the model.
- The runnable suite covers application isolation, changed-payload conflicts, durable attachment ownership, invalid content, API-key rejection, admin replay/auditing, Kafka publication/consumption and duplicate consumption after outbox cleanup, simulated delivery, forged Kafka input, and SMTP/Brevo message construction.
- Excluded existing `AuthenticationTests` and `NotificationApiTests` because their SQL Server fixtures require Docker. Equivalent new notification scenarios run against SQLite, but this does not replace testing SQL Server migrations/concurrency.
- No production database migrations, live provider sends or external deployments were performed.
- The workspace already contained trailing-whitespace differences in `EmailRequestedV1.cs` and `FileEmailTemplateRenderer.cs`; those pre-existing formatting changes were left intact.

Commands:

```bash
dotnet build Api/Api.csproj --no-restore --disable-build-servers -m:1
dotnet test MPDCApiTemplate.sln --no-restore --disable-build-servers -m:1 \
  --filter 'FullyQualifiedName!~Api.IntegrationTests.AuthenticationTests&FullyQualifiedName!~Api.IntegrationTests.NotificationApiTests'
dotnet ef migrations has-pending-model-changes --project Persistence --startup-project Api --no-build -- --Database:Provider SqlServer
```

