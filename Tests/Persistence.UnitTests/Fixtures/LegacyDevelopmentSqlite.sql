-- Schema of the pre-migration development database. Contains no local data.
CREATE TABLE "EmailNotifications" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_EmailNotifications" PRIMARY KEY,
    "ApplicationId" TEXT NOT NULL DEFAULT 'legacy',
    "BusinessReference" TEXT NULL,
    "RequestHash" TEXT NULL,
    "ContentJson" TEXT NULL,
    "MessageId" TEXT NOT NULL,
    "CorrelationId" TEXT NOT NULL,
    "IdempotencyKey" TEXT NOT NULL,
    "RecipientEmail" TEXT NOT NULL,
    "RecipientName" TEXT NULL,
    "SenderEmail" TEXT NOT NULL,
    "SenderName" TEXT NULL,
    "ReplyTo" TEXT NULL,
    "TemplateId" TEXT NOT NULL,
    "TemplateVariables" TEXT NOT NULL,
    "Subject" TEXT NULL,
    "Status" INTEGER NOT NULL,
    "Source" INTEGER NOT NULL,
    "RequestedAt" INTEGER NOT NULL,
    "ScheduledAt" INTEGER NULL,
    "SentAt" INTEGER NULL,
    "FailedAt" INTEGER NULL,
    "ProviderMessageId" TEXT NULL,
    "AttemptCount" INTEGER NOT NULL,
    "NextAttemptAt" INTEGER NULL,
    "LastErrorCode" TEXT NULL,
    "LastErrorMessage" TEXT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL,
    "OriginalTopic" TEXT NULL,
    "OriginalPartition" INTEGER NULL,
    "OriginalOffset" INTEGER NULL
);
CREATE TABLE "InboxMessages" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_InboxMessages" PRIMARY KEY,
    "MessageId" TEXT NOT NULL,
    "ConsumerName" TEXT NOT NULL,
    "Topic" TEXT NOT NULL,
    "Partition" INTEGER NOT NULL,
    "Offset" INTEGER NOT NULL,
    "ReceivedAt" INTEGER NOT NULL,
    "ProcessedAt" INTEGER NULL,
    "PayloadHash" TEXT NOT NULL
);
CREATE TABLE "NotificationAttachments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationAttachments" PRIMARY KEY,
    "ApplicationId" TEXT NOT NULL,
    "FileName" TEXT NOT NULL,
    "ContentType" TEXT NOT NULL,
    "Content" BLOB NOT NULL,
    "CreatedAt" INTEGER NOT NULL
);
CREATE TABLE "OutboxMessages" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_OutboxMessages" PRIMARY KEY,
    "EventType" TEXT NOT NULL,
    "SchemaVersion" INTEGER NOT NULL,
    "MessageKey" TEXT NOT NULL,
    "Topic" TEXT NOT NULL,
    "Payload" TEXT NOT NULL,
    "Headers" TEXT NOT NULL,
    "OccurredAt" INTEGER NOT NULL,
    "ProcessedAt" INTEGER NULL,
    "AttemptCount" INTEGER NOT NULL,
    "NextAttemptAt" INTEGER NOT NULL,
    "LastError" TEXT NULL,
    "CreatedAt" INTEGER NOT NULL
);
CREATE TABLE "Roles" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Roles" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Created" INTEGER NOT NULL,
    "CreatedBy" TEXT NULL,
    "LastModified" INTEGER NOT NULL,
    "LastModifiedBy" TEXT NULL
);
CREATE TABLE "NotificationDeliveryAttempts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationDeliveryAttempts" PRIMARY KEY,
    "NotificationId" TEXT NOT NULL,
    "AttemptNumber" INTEGER NOT NULL,
    "Provider" TEXT NOT NULL,
    "StartedAt" INTEGER NOT NULL,
    "CompletedAt" INTEGER NULL,
    "Outcome" INTEGER NOT NULL,
    "ProviderStatusCode" INTEGER NULL,
    "ProviderMessageId" TEXT NULL,
    "ErrorCategory" INTEGER NOT NULL,
    "ErrorCode" TEXT NULL,
    "SafeErrorMessage" TEXT NULL,
    "NextAttemptAt" INTEGER NULL,
    CONSTRAINT "FK_NotificationDeliveryAttempts_EmailNotifications_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES "EmailNotifications" ("Id") ON DELETE CASCADE
);
CREATE TABLE "NotificationReplays" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationReplays" PRIMARY KEY,
    "NotificationId" TEXT NOT NULL,
    "RequestedBy" TEXT NOT NULL,
    "RequestedAt" INTEGER NOT NULL,
    "PreviousAttemptCount" INTEGER NOT NULL,
    CONSTRAINT "FK_NotificationReplays_EmailNotifications_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES "EmailNotifications" ("Id") ON DELETE CASCADE
);
CREATE TABLE "Users" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "Email" TEXT NOT NULL,
    "FirstName" TEXT NOT NULL,
    "LastName" TEXT NOT NULL,
    "UserName" TEXT NOT NULL,
    "MobilePhone" TEXT NULL,
    "PasswordHash" TEXT NOT NULL,
    "LastLogin" INTEGER NULL,
    "RoleId" INTEGER NOT NULL,
    "Created" INTEGER NOT NULL,
    "CreatedBy" TEXT NULL,
    "LastModified" INTEGER NOT NULL,
    "LastModifiedBy" TEXT NULL,
    CONSTRAINT "FK_Users_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE RESTRICT
);
CREATE INDEX "IX_EmailNotifications_ApplicationId_BusinessReference" ON "EmailNotifications" ("ApplicationId", "BusinessReference");
CREATE UNIQUE INDEX "IX_EmailNotifications_ApplicationId_IdempotencyKey" ON "EmailNotifications" ("ApplicationId", "IdempotencyKey");
CREATE INDEX "IX_EmailNotifications_CorrelationId" ON "EmailNotifications" ("CorrelationId");
CREATE UNIQUE INDEX "IX_EmailNotifications_MessageId" ON "EmailNotifications" ("MessageId");
CREATE INDEX "IX_EmailNotifications_Status_NextAttemptAt" ON "EmailNotifications" ("Status", "NextAttemptAt");
CREATE UNIQUE INDEX "IX_InboxMessages_ConsumerName_MessageId" ON "InboxMessages" ("ConsumerName", "MessageId");
CREATE INDEX "IX_InboxMessages_Topic_Partition_Offset" ON "InboxMessages" ("Topic", "Partition", "Offset");
CREATE INDEX "IX_NotificationAttachments_ApplicationId_CreatedAt" ON "NotificationAttachments" ("ApplicationId", "CreatedAt");
CREATE UNIQUE INDEX "IX_NotificationDeliveryAttempts_NotificationId_AttemptNumber" ON "NotificationDeliveryAttempts" ("NotificationId", "AttemptNumber");
CREATE INDEX "IX_NotificationReplays_NotificationId_RequestedAt" ON "NotificationReplays" ("NotificationId", "RequestedAt");
CREATE INDEX "IX_OutboxMessages_MessageKey" ON "OutboxMessages" ("MessageKey");
CREATE INDEX "IX_OutboxMessages_ProcessedAt_NextAttemptAt" ON "OutboxMessages" ("ProcessedAt", "NextAttemptAt");
CREATE UNIQUE INDEX "IX_Roles_Name" ON "Roles" ("Name");
CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
CREATE INDEX "IX_Users_RoleId" ON "Users" ("RoleId");
CREATE UNIQUE INDEX "IX_Users_UserName" ON "Users" ("UserName");
