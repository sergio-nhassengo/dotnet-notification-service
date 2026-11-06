# Notification service integration

The service accepts email only through its REST API and authenticates TAN with an application API key. Sender settings, template permissions, content limits, and status queries are application scoped.

REST saves an email and outbox event atomically. Kafka is internal transport: consumption must match the durable REST record before making the email eligible for delivery. Unknown or mismatched events go to the DLQ through the outbox. Both publication and invalid-event handling are at least once. Run one replica and deliver one eligible email at a time, ordered by creation timestamp and ID.

The email stores current delivery state, retry timing, error details and the latest provider-call timestamp. Structured logs capture attempt details. Provider acceptance does not guarantee receipt, and a crash before result persistence can repeat a send.

Attachments are not supported. Templates use unversioned IDs and ship with build/publish output, including `payment-confirmed`.

See the [README](../README.md), [template catalogue](domain-notification-templates.md), and [Postman guide](../postman/README.md) for configuration and usage. The persistence migration is `20260910134430_SimplifyNotificationPersistence` and includes a compatibility path for existing development SQLite files.
