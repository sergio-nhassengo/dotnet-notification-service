# Notification service Postman tests

Import [the collection](MPDCApiTemplate.postman_collection.json) and [the local environment](MPDCNotificationService.Local.postman_environment.json). The collection covers authentication, all notification REST endpoints, every configured template/application pairing, validation, application isolation, attachments, idempotency, search, replay, and delivery observation.

## Setup and execution

1. Start the service and database. Set `baseUrl` to the API address (default `http://localhost:5290`). Use `EmailProvider:Provider=Fake` for a run that does not send real email. With Kafka enabled, start the broker and workers too.
2. Development API keys are already configured in `Api/appsettings.Development.json` and the Postman collection/environments. Restart the service in Development and re-import the updated Postman files. For other environments, configure `NotificationIntegration:Applications:<application>:ApiKeySha256` with the SHA-256 hex digest of your secret and put the plain secret into the matching Postman variable. Requests construct `Authorization: ApiKey <application>.<secret>`.

   | Application | Postman variable | Development secret |
   | --- | --- | --- |
   | `otc-api` | `otcApiKey` | `dev-otc-postman-2026` |
   | `weighbridge-api` | `weighbridgeApiKey` | `dev-weighbridge-postman-2026` |
   | `tan-webservice` | `tanApiKey` | `dev-tan-postman-2026` |

3. Set `recipientEmail` and `ccEmail` to controlled test addresses when using a real provider. Set the Postman `provider` variable to match the server (`Fake`, `SMTP`, or `Brevo`); this variable does not change server configuration.
4. Development startup seeds the Admin account `dev.admin` with password `Dev-Postman-2026!`. These defaults are populated in the collection and environments. Admin login captures `adminToken`. You can instead supply an existing admin token as a collection variable. Only development test credentials are included in the files. The seeder runs only in Development and preserves existing accounts on restart.
5. Run the complete collection in Collection Runner, in order, with one iteration. The first request creates a fresh run ID and clears captured fixture IDs. Requests generate unique idempotency keys; retries intentionally reuse the baseline body. To rerun only a folder, first run its prerequisites.
6. Review **skipped requests** as well as failures. Missing credentials or prepared fixture IDs cause dependent requests to be skipped with a console message. Skipped scenarios have not been verified. A failed prerequisite can also prevent downstream coverage.

The run creates users, notifications, delivery attempts, and attachment records. There is no cleanup API. Use an isolated test database and reset it through your normal development workflow when needed. Generated user credentials are test-only. Do not export captured tokens or populated secret values into source control.

The environment leaves captured run IDs and tokens to collection variables to avoid shadowing them. Avoid creating environment variables with names such as `notificationId`, `baselineBody`, `runId`, or `<application>AttachmentId`.

## Scenario groups

| Folder | Coverage |
| --- | --- |
| 01 | Liveness, readiness, OpenAPI, fresh run namespace |
| 02 | Admin/user login, registration, duplicate/invalid registration, invalid login, missing/malformed/unknown/wrong credentials across protected endpoints |
| 03 | Durable multipart upload for OTC, Weighbridge, TAN; captures attachment IDs |
| 04 | Text/HTML, multiple recipients, legacy recipient form, Unicode, scheduling, attachment send, boundary sizes, identical retries, changed-content conflicts, cross-application idempotency |
| 05 | Every application allowlist entry, using actual template metadata examples and required document references |
| 06 | Field lengths, null collections, recipient formats/counts, header injection, raw/template conflicts, body size, attachment IDs/content IDs/duplicates, dates, malformed JSON, content type |
| 07 | Required/empty/null/oversized variables, invalid/unallowed template IDs, rendered subject limits, subject overrides, HTML input acceptance, required documents |
| 08 | Attachment ownership, empty/unsupported/path/long-name uploads, CSV, missing file, provider-dependent inline attachment acceptance |
| 09 | Status identity/attempts, unknown IDs, application isolation, all search filters, pagination, invalid queries, admin reads |
| 10 | API key/user replay denial, unknown/non-replayable conflicts, prepared failed-message replay, content revalidation |
| 11 | Optional bounded polling to terminal delivery state |
| 12 | Optional restricted/disabled application policies and missing template files |
| 13 | Optional server request-size limits and diagnostics endpoint |

All template requests use `templateId` with **no template version**. The `/api/v1` route is the existing HTTP route and does not version templates. Unknown template IDs currently return **401**, because allowlist checks run before file rendering; an allowed template missing on disk returns **400**.

Default expectations match `Api/appsettings.json`: 50 recipients, 5 attachments, 5 MiB attachment limit, raw content and subject overrides enabled. If your configuration differs, use a matching test deployment or adjust the relevant assertions. Positive attachment fixtures are small text documents transmitted as raw multipart bodies, so no file picker or local filesystem access is needed. They test storage and references; they do not certify PDF/image rendering.

## Optional cases

- **Delivery:** set `runDeliveryPolling=true`, `maxPollAttempts` (default 30), and `pollDelayMs` (default 1000). Polling works in Collection Runner and is bounded. `expectedTerminalStatus` defaults to `Sent`. Set a different value only when deliberately testing a failure-producing provider configuration. The Fake provider always succeeds. `Sent` means provider acceptance, not inbox delivery.
- **Replay:** supply `failedNotificationId` for a `PermanentlyFailed` or `DeadLettered` notification with valid current content and an admin token. Replay changes its state. To repeat the test, prepare another failed notification. Supply `replayInvalidNotificationId` for a separate failed notification with missing required files/attachments or a disabled application; expected status is 400. An unallowed template produces 401 instead and is not the fixture for this request.
- **Restricted applications:** set `runRestrictedConfiguration=true`, `restrictedAppId`, `restrictedApiKey`, and `restrictedTemplateId`. Configure that server application with raw content disabled, subject overrides disabled, and `MaximumAttachments=0`. Set `missingTemplateId` to an OTC-allowed ID whose template files are absent. Set `disabledAppId`/`disabledApiKey` for a separate disabled application. These requests do not edit server configuration.
- **Large requests:** set `runOversizedRequests=true`. The requests generate their large bodies at runtime, including 5 MiB and 12 MiB uploads. Expected 413 responses require the hosting server to enforce request limits; run directly against Kestrel. Proxy-specific limits or responses can differ.
- **Diagnostics:** set `runDiagnostics=true`. Set `diagnosticsExpectedStatus=500` in Development or `404` elsewhere. This deliberately exercises the exception endpoint.

## Scenarios requiring infrastructure or separate tests

Postman HTTP requests cannot directly publish Kafka messages, alter database state, observe email rendering, or configure provider responses. Pair this collection with the repository integration tests and a controlled deployment to verify:

- Kafka malformed/unsupported schema messages, forged application claims, key/payload mismatch, inbox deduplication, outbox cleanup and recovery after crashes.
- Concurrent idempotent submissions and database uniqueness races (Collection Runner is sequential).
- Provider timeout, rate limiting, transient/permanent failures, retry backoff/exhaustion, worker restart recovery, and replay audit rows.
- Combined attachment bytes exceeding the application total while each uploaded file is individually valid; prepare multiple large owned attachment IDs and send them together, expecting 400.
- Missing/corrupt template metadata, broken database/broker readiness, and disabled legacy JWT access under alternate configurations.
- Actual rendered HTML escaping, document contents, SMTP inline image rendering, and downstream inbox delivery. HTTP acceptance alone cannot prove these.

## Validation performed

The generated JSON was parsed, embedded JavaScript compiled, and pre-request scripts exercised with simulated variables. Normal JSON/multipart bodies and variable resolution were checked. This is artifact validation, not a live Postman run against your deployment.
