using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyNotificationPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                UpgradeSqlite(migrationBuilder);
                return;
            }
            migrationBuilder.AddColumn<DateTimeOffset>(name: "LastAttemptAt", table: "EmailNotifications", type: "datetimeoffset", nullable: true);
            migrationBuilder.Sql("""
                UPDATE n SET LastAttemptAt = a.StartedAt
                FROM EmailNotifications n
                OUTER APPLY (SELECT MAX(StartedAt) AS StartedAt FROM NotificationDeliveryAttempts WHERE NotificationId = n.Id) a;
                UPDATE EmailNotifications SET NextAttemptAt = CreatedAt WHERE Status IN (0, 1);
                UPDATE EmailNotifications SET Status = 3, NextAttemptAt = COALESCE(NextAttemptAt, CreatedAt) WHERE Status = 2;
                """);
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "NotificationDeliveryAttempts");

            migrationBuilder.DropTable(
                name: "NotificationReplays");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "OriginalOffset",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "OriginalPartition",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "OriginalTopic",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "ReplyTo",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(name: "ScheduledAt", table: "EmailNotifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
                throw new NotSupportedException("SQLite rollback across this data-removing migration is not supported. Restore a database backup instead.");
            migrationBuilder.DropColumn(name: "LastAttemptAt", table: "EmailNotifications");
            migrationBuilder.AddColumn<DateTimeOffset>(name: "ScheduledAt", table: "EmailNotifications", type: "datetimeoffset", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "OutboxMessages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "OriginalOffset",
                table: "EmailNotifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalPartition",
                table: "EmailNotifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalTopic",
                table: "EmailNotifications",
                type: "nvarchar(249)",
                maxLength: 249,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "EmailNotifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyTo",
                table: "EmailNotifications",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedAt",
                table: "EmailNotifications",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "EmailNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Offset = table.Column<long>(type: "bigint", nullable: false),
                    Partition = table.Column<int>(type: "int", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(249)", maxLength: 249, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorCategory = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProviderStatusCode = table.Column<int>(type: "int", nullable: true),
                    SafeErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryAttempts_EmailNotifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "EmailNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationReplays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousAttemptCount = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReplays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationReplays_EmailNotifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "EmailNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ConsumerName_MessageId",
                table: "InboxMessages",
                columns: new[] { "ConsumerName", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Topic_Partition_Offset",
                table: "InboxMessages",
                columns: new[] { "Topic", "Partition", "Offset" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_NotificationId_AttemptNumber",
                table: "NotificationDeliveryAttempts",
                columns: new[] { "NotificationId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReplays_NotificationId_RequestedAt",
                table: "NotificationReplays",
                columns: new[] { "NotificationId", "RequestedAt" });
        }
        // SQLite previously used EnsureCreated, so existing files have no migration history.
        // IF NOT EXISTS also supports a fresh database. All DDL/data changes and migration
        // history are committed by EF's migration transaction; no data rows are seeded here.
        private static void UpgradeSqlite(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "EmailNotifications" (
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
            CREATE TABLE IF NOT EXISTS "InboxMessages" (
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
            CREATE TABLE IF NOT EXISTS "NotificationAttachments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationAttachments" PRIMARY KEY,
                "ApplicationId" TEXT NOT NULL,
                "FileName" TEXT NOT NULL,
                "ContentType" TEXT NOT NULL,
                "Content" BLOB NOT NULL,
                "CreatedAt" INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "OutboxMessages" (
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
            CREATE TABLE IF NOT EXISTS "Roles" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Roles" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Description" TEXT NULL,
                "Created" INTEGER NOT NULL,
                "CreatedBy" TEXT NULL,
                "LastModified" INTEGER NOT NULL,
                "LastModifiedBy" TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS "NotificationDeliveryAttempts" (
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
            CREATE TABLE IF NOT EXISTS "NotificationReplays" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationReplays" PRIMARY KEY,
                "NotificationId" TEXT NOT NULL,
                "RequestedBy" TEXT NOT NULL,
                "RequestedAt" INTEGER NOT NULL,
                "PreviousAttemptCount" INTEGER NOT NULL,
                CONSTRAINT "FK_NotificationReplays_EmailNotifications_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES "EmailNotifications" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "Users" (
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
            CREATE INDEX IF NOT EXISTS "IX_EmailNotifications_ApplicationId_BusinessReference" ON "EmailNotifications" ("ApplicationId", "BusinessReference");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmailNotifications_ApplicationId_IdempotencyKey" ON "EmailNotifications" ("ApplicationId", "IdempotencyKey");
            CREATE INDEX IF NOT EXISTS "IX_EmailNotifications_CorrelationId" ON "EmailNotifications" ("CorrelationId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmailNotifications_MessageId" ON "EmailNotifications" ("MessageId");
            CREATE INDEX IF NOT EXISTS "IX_EmailNotifications_Status_NextAttemptAt" ON "EmailNotifications" ("Status", "NextAttemptAt");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_InboxMessages_ConsumerName_MessageId" ON "InboxMessages" ("ConsumerName", "MessageId");
            CREATE INDEX IF NOT EXISTS "IX_InboxMessages_Topic_Partition_Offset" ON "InboxMessages" ("Topic", "Partition", "Offset");
            CREATE INDEX IF NOT EXISTS "IX_NotificationAttachments_ApplicationId_CreatedAt" ON "NotificationAttachments" ("ApplicationId", "CreatedAt");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationDeliveryAttempts_NotificationId_AttemptNumber" ON "NotificationDeliveryAttempts" ("NotificationId", "AttemptNumber");
            CREATE INDEX IF NOT EXISTS "IX_NotificationReplays_NotificationId_RequestedAt" ON "NotificationReplays" ("NotificationId", "RequestedAt");
            CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_MessageKey" ON "OutboxMessages" ("MessageKey");
            CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_ProcessedAt_NextAttemptAt" ON "OutboxMessages" ("ProcessedAt", "NextAttemptAt");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Roles_Name" ON "Roles" ("Name");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");
            CREATE INDEX IF NOT EXISTS "IX_Users_RoleId" ON "Users" ("RoleId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_UserName" ON "Users" ("UserName");
            ALTER TABLE "EmailNotifications" ADD COLUMN "LastAttemptAt" INTEGER NULL;
            UPDATE "EmailNotifications" SET "LastAttemptAt" = (
                SELECT MAX("StartedAt") FROM "NotificationDeliveryAttempts" WHERE "NotificationId" = "EmailNotifications"."Id");
            UPDATE "EmailNotifications" SET "NextAttemptAt" = "CreatedAt" WHERE "Status" IN (0, 1);
            UPDATE "EmailNotifications" SET "Status" = 3, "NextAttemptAt" = COALESCE("NextAttemptAt", "CreatedAt") WHERE "Status" = 2;
            DROP TABLE "InboxMessages";
            DROP TABLE "NotificationDeliveryAttempts";
            DROP TABLE "NotificationReplays";
            ALTER TABLE "EmailNotifications" DROP COLUMN "Source";
            ALTER TABLE "EmailNotifications" DROP COLUMN "RequestedAt";
            ALTER TABLE "EmailNotifications" DROP COLUMN "ScheduledAt";
            ALTER TABLE "EmailNotifications" DROP COLUMN "ProviderMessageId";
            ALTER TABLE "EmailNotifications" DROP COLUMN "ReplyTo";
            ALTER TABLE "EmailNotifications" DROP COLUMN "OriginalTopic";
            ALTER TABLE "EmailNotifications" DROP COLUMN "OriginalPartition";
            ALTER TABLE "EmailNotifications" DROP COLUMN "OriginalOffset";
            ALTER TABLE "OutboxMessages" DROP COLUMN "EventType";
            ALTER TABLE "OutboxMessages" DROP COLUMN "SchemaVersion";
            """);
    }
}
