# Notification service Postman tests

Import `MPDCApiTemplate.postman_collection.json` and the Local or Docker environment. Start the service, database and Kafka; use `EmailProvider:Provider=Smtp` with a local mail capture server for tests.

Requests use `Authorization: ApiKey <application-id>.<secret>`. The local development digests match these test-only secrets:

| Application | Variable | Local secret |
| --- | --- | --- |
| `otc-api` | `otcApiKey` | `dev-otc-postman-2026` |
| `weighbridge-api` | `weighbridgeApiKey` | `dev-weighbridge-postman-2026` |
| `tan-webservice` | `tanApiKey` | `dev-tan-postman-2026` |

Set `baseUrl`, `recipientEmail` and `ccEmail`. For other environments supply your own secrets and configure their SHA-256 digests on the service. The `provider` variable controls expected inline-attachment behavior; it does not configure the server.

Run the collection in order. The first request resets the run ID and captured fixtures. Each independent request gets a unique idempotency key; baseline retries deliberately reuse the saved request. Coverage includes missing/invalid API keys, positive and negative application isolation, attachment ownership, all configured template/application pairs including unversioned `payment-confirmed`, input validation, status and search.

Set `runDeliveryPolling=true` to observe current delivery state until `expectedTerminalStatus` (normally `Sent`). Configure `maxPollAttempts` and `pollDelayMs` for your environment. Failed prerequisite uploads, missing credentials and optional fixtures can skip requests: review skips separately from passed assertions. Configuration-dependent and oversized-request cases are opt-in.

The tests create notifications and attachments. Attachment cleanup deletes files older than `NotificationAttachments:RetentionDays`; this period must exceed the maximum expected delivery/retry duration. Use a dedicated development database. Captured IDs stay in collection variables; do not shadow them in environment variables. Do not export real secrets into source control.
