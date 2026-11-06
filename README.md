# MPDC notification service

A .NET 10 email service with REST submission, application API keys, EF Core persistence, an outbox, and Kafka as an internal transport. Run exactly one service replica.

An authenticated POST validates the application's permissions, recipients, content and templates. It saves a `Pending` email and its Kafka outbox message together in one database transaction. The publisher forwards durable outbox messages to Kafka and marks them processed only after broker acknowledgement.

The consumer accepts only events matching a durable REST-created email by `MessageId`, `ApplicationId`, `IdempotencyKey` and `RequestHash`. It changes `Pending` to `Queued`; an already accepted state is a duplicate. Database persistence must succeed before the source offset is committed. A failed save or commit retries the same record before consuming another. Unknown or mismatched events produce a durable dead-letter outbox message; direct Kafka submission is unsupported.

Dead-letter delivery is at least once. An invalid event may appear more than once if the service crashes after DLQ publication but before committing the original offset. A crash between outbox publication and marking it processed can also repeat publication. Restrict broker access to this service.

The delivery worker selects one currently eligible email (`Queued` or `RetryScheduled`, with `NextAttemptAt <= now`), ordered by `CreatedAt` then `Id`. New emails have `NextAttemptAt = CreatedAt`. Automatic retries use delays of 1 minute, 5 minutes, 15 minutes and then 1 hour, respecting a longer provider retry delay; the default limit is five attempts. Each result updates the email's current state and any terminal-failure outbox message in one save. Structured Serilog events contain attempt details. `LastAttemptAt` records the latest provider call; content-preparation failures still count toward the retry limit.

`Sent` means the configured provider accepted the email, not that the recipient received it. If provider acceptance succeeds but saving the result fails, the email may be sent again. SMTP does not guarantee deduplication.

## API

Send `Authorization: ApiKey <application-id>.<secret>`. Configure the SHA-256 hexadecimal digest of each secret under `NotificationIntegration:Applications:<application-id>:ApiKeySha256`. Application identity comes from authentication. Each application controls its sender, allowed template IDs, raw-content permission, subject override permission and recipient/attachment limits. Queries and attachment references are restricted to the authenticated application.

| Endpoint | Purpose |
| --- | --- |
| `POST /api/v1/notifications/email` | Accept an email; returns 202 and a status URL |
| `GET /api/v1/notifications/{notificationId}` | Read current delivery state |
| `GET /api/v1/notifications` | Search by correlation ID or idempotency key |
| `GET /health/live` | Process liveness |
| `GET /health/ready` | Database readiness |
| `GET /swagger` | Interactive API documentation |

```json
{
  "idempotencyKey": "payment-123-confirmation",
  "correlationId": "payment-123",
  "to": [{ "email": "customer@example.com", "name": "Customer" }],
  "templateId": "payment-confirmed",
  "variables": { "customerName": "Customer", "paymentReference": "PAY-123" }
}
```

Use a new idempotency key for each independent notification. Repeating an identical request returns the same IDs; changing its content with the same application/key returns 409. Both `MessageId` and `(ApplicationId, IdempotencyKey)` have unique database indexes. The original single `recipient` form is also accepted; use either `recipient` or `to`. Raw content uses `subject` and `htmlBody` and/or `textBody` instead of a template. `cc` is an optional array. Attachments are not supported.

Templates are unversioned directories under `Api/Templates`. Templates are scoped exclusively to `tan-webservice`. See the [domain template catalogue](docs/domain-notification-templates.md) for the other templates. Files are copied into build and publish output.

## Run locally

Install the .NET 10 SDK and start Kafka. `Api/appsettings.Development.json` selects SQLite and contains development-only application key digests. The [Postman instructions](postman/README.md) list the matching local secrets. Configure SMTP with a local mail capture server for development, or supply the SmtpRelay credentials before starting the service.

```bash
dotnet restore
dotnet build MPDCApiTemplate.sln
dotnet run --project Api
```

Development startup calls `Database.MigrateAsync()`. The new migration upgrades the previous development SQLite schema, including files without migration metadata, without deleting notification or outbox records. The `RemoveNotificationAttachments` migration drops the attachments table and its stored files. Back up an existing file before upgrading. SQLite keeps its existing binary timestamp representation. Historical SQL Server migrations are bypassed for SQLite; the new migration supplies its compatible schema and upgrade. The shared snapshot is generated for SQL Server, so SQLite's expected provider model difference is explicitly excluded from the pending-model warning. SQLite rollback across this migration requires restoring a backup.

Production schema changes should run as a deployment step, before starting the service:

```bash
dotnet ef database update --project Persistence --startup-project Api
dotnet ef migrations list --project Persistence --startup-project Api
```

Set `Database:Provider` to `SqlServer` or `Sqlite` and `ConnectionStrings:Default` accordingly. Use `EmailProvider:Provider` = `Smtp` or `SmtpRelay`; settings are under `Smtp` and `SmtpRelay`, respectively. `Kafka:Enabled=false` disables broker workers for isolated development/tests; REST still persists pending emails and outbox rows, and delivery resumes only after Kafka is enabled. Health readiness checks database access, not broker connectivity.

`compose.yaml` uses the external `kafka_default` network and a durable SQLite volume. Configure production secrets outside source control. Keep Serilog console/file logging; an optional SQL log sink uses `ConnectionStrings:Logging`.

## Projects and verification

`Domain` owns entities. `Application` owns commands and queries (`Features/Notifications/{Commands|Queries}/<Name>/`), the interfaces Infrastructure implements (`Notifications/Abstractions`), shared records (`Notifications/Models`), the Kafka message contracts (`Notifications/Contracts`), the idempotency fingerprint (`Notifications/Idempotency`) and `IApplicationDbContext`. Delivery and Kafka acceptance rules are the `DeliverEmail` and `AcceptEmailRequested` commands; workers only poll, consume and call them. `Infrastructure` owns workers, email providers, templates, Kafka clients and options; it has no ASP.NET Core dependency. `Persistence` owns EF configuration and migrations. `Api` hosts the REST endpoints, API-key authentication (which supplies `INotificationCaller`) and CORS. Handlers and workers use `IApplicationDbContext` directly.

```bash
dotnet restore
dotnet build MPDCApiTemplate.sln
dotnet test MPDCApiTemplate.sln
dotnet ef migrations list --project Persistence --startup-project Api
```

The tests cover application isolation, persistence, Kafka acceptance/commit behavior, delivery, cleanup and migrations. SQL Server integration tests require Docker. Import the Postman collection and environment for REST verification.

### Port Maputo SMTP relay provider

Select `EmailProvider__Provider=SmtpRelay` and configure these environment variables (or equivalent configuration keys):

```text
SmtpRelay__BaseUrl=https://uatinport.portmaputo.com/api/
SmtpRelay__Username=<login username>
SmtpRelay__Password=<login password>
SmtpRelay__CreatedBy=user@example.com
SmtpRelay__TokenProperty=accessToken
SmtpRelay__TimeoutSeconds=30
NotificationIntegration__Applications__tan-webservice__SenderEmail=tansystem@portmaputo.com
```

Keep credentials in environment variables or a secret store. Each delivery first posts `username` and `password` to `login`, then posts `from`, `to`, `cc`, `subject`, `text`, `html`, `createdBy` and an empty `attachments` array to `SMTPRelay`. The sender comes from the application's sender configuration; `createdBy` is the configured relay service identity. SmtpRelay is the default provider and requires credentials and CreatedBy at startup.

The adapter extracts the top-level `accessToken` from the login response and sends it as `Authorization: Bearer <accessToken>`. Other login response fields are ignored. Set `TokenProperty` to a dot-separated path such as `data.accessToken` if necessary. Multiple recipients are comma-separated strings; confirm this convention with the relay API before using recipient groups. Non-empty attachments are rejected during preflight and delivery until the relay attachment schema is available. Successful HTTP status codes are treated as acceptance; no response body contract was supplied. Network errors, timeouts, HTTP 408/429 and server errors use the existing delivery retry policy; the relay's deduplication behavior is unknown, so an ambiguous timeout can result in duplicate delivery. No live emails are sent by the provider tests.
