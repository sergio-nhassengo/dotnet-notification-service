# TAN webservice notification integration plan

Analysis date: 14 September 2026. TAN checkout: `2ed838c4`, including the recent receipt notification changes. This plan targets the current .NET notification service in `mpdcapitemplate`, including its uncommitted working-tree changes. The sibling `notification-service` directory contains a different, older Node implementation; its `/sendEmail` contract is not the integration target assumed here. No application code was changed or live email sent during this analysis.

## Findings in TAN

- `Infrastructure/Services/EmailService.cs` implements synchronous SMTP for HTML messages, custom senders, CC, and attachments. `API/Startup.cs:156` registers it as `IEmailService`. Callers own business rules, recipient selection and HTML generation.
- `ReceiptTransactionNotification.SendReceiptTransactionAfterSecondMass` selects up to 40 completed receipts with an exit date in 2026 or later and `WasTicketSentToTransporter == false`. It attempts time-discrepancy, customer receipt, nett-deviation, and agent/transporter PDF emails, then sets the single flag. Helper methods catch failures or return early, so the flag can be set without all emails succeeding. Previously successful messages can also repeat if the final database save fails.
- Latest receipt behavior must be preserved: customer nett threshold must be positive and absolute mine/nett difference strictly greater than that threshold; Transfer To Port AND Transfer To Vessel use the external-storage ticket report; transporter address selection falls back to the group address when necessary.
- Ticket sending reuses one `MailMessage` for agent and transporter. The SMTP adapter appends `To` recipients, so the second send can include the agent again. Migration should create separate immutable requests for each audience.
- `API/Startup.cs` enables `SendReceiptTransactionJob` through `SendTransporterPdfTicket`. The older `SendTicketToTransporter` registration is commented out, but the transporter transaction-summary job still delegates to the external ticket API. Do not reactivate retired jobs.
- Other email families include TIP/payment updates, RAN events, truck release/agent changes, account creation/reset, operational failures, data-integrity spreadsheets and customs reports.
- `SendCbrEmail` delegates to INPORT `/api/Emails/notify-cbr/{id}`; `SendCbrMonitoringEmail` delegates prepared HTML to INPORT `/api/SMTPRelay`. These are distinct dependencies, not SMTP calls. The former's recipient/content behavior is outside the inspected repository.
- A SendGrid implementation is registered, but no active `SendEmailAsync` call was found. The in-app `GetNotifications` query is separate from outbound email.

## Target design

TAN business logic / existing worker → awaited HTTP call → notification-service REST/database/outbox → internal Kafka → email provider.

Use a simple direct integration: no TAN notification-intent table, local outbox, or new dispatch worker. Durable storage and delivery retries remain inside notification-service. Existing TAN business records and workers remain in place. TAN makes one attempt per request and logs failures; it performs no automatic HTTP retry, delayed resubmission or SMTP fallback.

TAN retains event eligibility, recipients, domain data and SSRS report generation. Notification-service owns template rendering, configured sender, attachments, provider delivery, delivery retries and status. TAN should not publish directly to Kafka or reference the service's .NET 10 projects from its .NET 8 projects; use HTTP DTOs.

The service provides API-key authentication, `POST /api/v1/notifications/email`, attachment upload, status lookup, idempotency and TAN templates. Submit each notification independently with its own stable idempotency key. Upload shared documents once and reuse their attachment IDs across the relevant requests.

Important compatibility gap: TAN currently selects `EmailSender`, `EmailNoreplySender`, and custom senders, whereas the service configures one sender per application. Inventory actual sender use before cutover. Standardize on the configured TAN sender if acceptable; otherwise add a validated, application-scoped sender-profile feature before migrating affected flows. Do not silently discard sender overrides.

## Implementation sequence

### 1. Freeze the contract and configuration

- Pin the reviewed notification-service revision before deployment; the present working tree contains substantial pending changes.
- Add TAN options for base URL, application ID `tan-webservice`, secret, timeout and per-workflow transport selection. Store secrets outside source control.
- Configure service template permissions, sender, recipient/attachment limits and raw-content/subject-override permissions. Prefer existing templates; allow raw HTML only for unmapped legacy content requiring exact parity.
- Use `Authorization: ApiKey tan-webservice.<secret>`. Verify authentication, application isolation and maximum production report sizes using SMTP with a local mail capture server.

### 2. Add a direct HTTP client

- Add an asynchronous `INotificationClient` implemented with a typed `HttpClient` in Infrastructure. Support attachment upload, email submission and lookup by idempotency key. Configure authentication, timeout and cancellation centrally.
- Existing handlers and workers choose recipients and templates, then await the HTTP call. Use existing event identifiers to construct keys that distinguish workflow and audience; recurring notifications also need an event/version or report period.
- For attachments, generate the report in TAN, upload it to `POST /api/v1/notifications/attachments`, and pass the returned ID to the email endpoint. Reuse the uploaded ID for related emails during that execution.
- Treat HTTP 202 as durable acceptance by notification-service, not delivery. Log the returned notification ID, correlation ID and key without message bodies or secrets. No automatic TAN delivery-status polling is required; use the service API for investigation.
- Make one HTTP attempt per upload or email submission. Disable automatic retry policies, including for connection failures, timeouts, 429 and server errors. Log validation/authentication failures and 409 responses without retrying them.
- Log failures using the workflow, correlation ID, audience, idempotency key and safe HTTP/error details. Do not include bodies, passwords or API keys. A timeout can mean acceptance is unknown; record that uncertainty without resubmitting or automatically checking status.
- Return an explicit acceptance/failure result to the caller and let the business workflow continue after logging a notification failure. Do not fail a completed business operation in a way that triggers an automatic resend. If attachment upload fails, log the failure and skip dependent email submissions; do not send a required-attachment email without its document.
- Existing polling workers must not pick failed notifications up again on the next run. There is no TAN retry queue, automatic recovery or SMTP fallback. Notifications that were not accepted may remain unsent. Any later investigation or resubmission is an explicit manual action; check the stable key in notification-service first when acceptance is uncertain.
- Delivery retries for requests already accepted remain entirely inside notification-service. TAN process interruption before acceptance can lose a notification under this simple design.

### 3. Pilot the updated receipt workflow

| TAN action | Existing service template | Separate email key |
| --- | --- | --- |
| Customer receipt email | `tan-receipt-completed` | Receipt + customer notification |
| Time between weighings below configured minimum | `tan-receipt-time-discrepancy` | Receipt + timing alert |
| Positive threshold exceeded | `tan-receipt-nett-deviation` | Receipt + deviation alert |
| PDF to agent | `weighbridge-ticket` | Receipt + agent audience |
| PDF to transporter | `weighbridge-ticket` | Receipt + transporter audience |

- Preserve current year filter, settings, recipient fallback and report choice. Use deterministic polling order and prevent overlapping execution of the receipt worker.
- Generate the PDF in TAN using existing SSRS credentials; upload it once and reuse its attachment ID for the two independent audience requests. Never serialize `MailMessage` or pass a process-local report stream to the remote service.
- Keep the existing receipt polling worker, but change its selection marker to mean “notification processing claimed”, not “email sent” or “all emails accepted”. Audit all readers of `WasTicketSentToTransporter` and rename/reuse the existing marker consistently; do not introduce a notification-intent table.
- Persist the processing claim atomically before report upload or email submission. Only the worker that successfully claims the receipt processes it. If claim persistence fails, log and perform no notification calls. Leave the marker set after submission failures so later polling runs cannot retry those notifications. This deliberately means a crash after claiming can leave emails unsent; the marker is not proof of an HTTP attempt or delivery.
- Return acceptance/failure from helper methods and log each outcome separately. Make at most one submission per applicable email, with separate keys for customer, timing, deviation, agent and transporter audiences. Continue independent emails after a failure; skip dependent ticket emails if PDF preparation/upload fails. Do not reset the processing marker on any email failure.
- Do not enable the old SMTP ticket worker alongside the migrated receipt worker.
- Select a cutover boundary. Historical `true` flags do not prove every email succeeded; do not automatically resend them. Handle any repair backlog explicitly.

### 4. Migrate remaining families incrementally

| Family | Templates / approach |
| --- | --- |
| TIP and bank payments | `tan-tip-generated`, `tan-tip-payment-submitted`, `tan-tip-payment-confirmed`, `tan-tip-payment-failed`; preserve payment rules and CC |
| Rail events | `rail-consignment-alert`, `tan-wagons-submitted`, `tan-wagons-updated`, `tan-wagons-deleted`; keys identify each event, not just the wagon |
| Truck and CBR-local events | Existing agent allocation, release-request and vessel-change templates; preserve Operations approval meaning |
| Reports | `data-integrity-report`, `weighbridge-transactions-report`, `tan-trips-history-report`; keep generation local and upload bytes |
| Operational failures | `tan-operational-failure` and SEW-specific templates; safe summaries, bounded frequency, independent logs/monitoring |
| Accounts | Existing account templates; restrict persisted password access/retention and exclude credentials from logs |
| Generic email endpoint | `tan-message` for plain text or explicitly permitted raw HTML |

Convert synchronous send call sites to awaited HTTP submission in each migrated workflow. Keep `IEmailService` temporarily for remaining SMTP routes and separate its INPORT responsibilities into a dedicated integration client. Avoid blocking HTTP through `.Result` or fire-and-forget sends.

Migrate INPORT SMTP relay messages once sender/content parity is verified. Leave INPORT CBR notification delegation and the external ticket-summary endpoint until their downstream content, report generation and recipient rules are inspected. A prepared template alone is insufficient to replace those integrations.

### 5. Validate and release

- Test receipt threshold boundaries (zero, equality, positive exceedance), timing boundaries, year eligibility, both external-storage report types, transporter fallback, missing configuration and separate agent/transporter recipients.
- Test service unavailability, timeouts/lost responses, 429, server errors, changed-content 409 and partial receipt acceptance. Verify exactly one request attempt, safe failure logging, continued business processing and no resubmission on later worker runs. Verify atomic receipt claiming, no notification calls after a failed claim save, and no repeat after restarting a worker. Verify failed PDF upload skips dependent ticket submissions.
- Test PDF/XLSX contents, limits, ownership and expiry. Upload near submission; preserve the accepted request's attachment identity. Expired attachments on accepted messages require an explicit remediation path, not a silently changed retry.
- Verify template variables against `docs/domain-notification-templates.md`: all required variables are nonempty strings, ordinary values are HTML-encoded, and report templates require attachments.
- Exercise provider failure/retry and terminal status using isolated provider integration tests and then controlled test recipients. Inspect generated emails against current behavior.
- Deploy the current service with exactly one replica, as required by its delivery design. Set attachment retention beyond backlog, downtime and retry duration; cleanup currently includes referenced uploads. Monitor Kafka/outbox lag separately because readiness checks only the database.
- Roll out per workflow. Never dual-send to real recipients. Rollback routes only new, unaccepted work to SMTP; reconcile uncertain acceptance by stable key/status first. Accepted messages remain owned by notification-service.
- Monitor TAN submission errors, receipt notification processing failures, service delivery failures and attachment preparation errors. Provider acceptance (`Sent`) does not prove inbox delivery; a crash after provider acceptance can still duplicate mail.

## Completion criteria

All migrated workflows retain business triggers and recipient rules; accepted emails can be located by stable key and correlation ID; attachments and sender behavior match the agreed contract; TAN makes no automatic retries or later worker resubmissions; submission failures are logged and any recovery is explicitly manual; and no migrated path still sends SMTP or delegates email unexpectedly. Deferred external integrations are listed explicitly with their owner and remaining discovery work.

## Source references

- TAN: `Application/Interfaces/IEmailService.cs`, `Infrastructure/Services/EmailService.cs`, `Infrastructure/Services/ReceiptTransactionNotification.cs`, `API/Workers/SendReceiptTransactionJob.cs`, `API/Startup.cs`, `Infrastructure/Cbr/CbrService.cs`.
- Notification service: `README.md`, `Api/Controllers/NotificationsController.cs`, `Api/Controllers/NotificationAttachmentsController.cs`, `Infrastructure/Notifications/Integration/NotificationApplications.cs`, `docs/domain-notification-templates.md`, `docs/notification-service-integration-report.md`.

This is a source-based integration plan. No build, runtime connectivity, deployed configuration or delivery behavior was verified in this planning task.
