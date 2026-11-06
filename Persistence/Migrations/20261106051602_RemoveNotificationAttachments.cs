using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotificationAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite schemas come from SimplifyNotificationPersistence's raw SQL, which created this
            // table with IF NOT EXISTS; drop it the same way so older files without it still upgrade.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("""
                    DROP TABLE IF EXISTS "NotificationAttachments";
                    """);
                return;
            }

            migrationBuilder.DropTable(
                name: "NotificationAttachments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                // Recreates the empty table as SimplifyNotificationPersistence defined it. Dropped files are not restored.
                migrationBuilder.Sql("""
                    CREATE TABLE "NotificationAttachments" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationAttachments" PRIMARY KEY,
                        "ApplicationId" TEXT NOT NULL,
                        "FileName" TEXT NOT NULL,
                        "ContentType" TEXT NOT NULL,
                        "Content" BLOB NOT NULL,
                        "CreatedAt" INTEGER NOT NULL
                    );
                    CREATE INDEX "IX_NotificationAttachments_ApplicationId_CreatedAt" ON "NotificationAttachments" ("ApplicationId", "CreatedAt");
                    """);
                return;
            }

            migrationBuilder.CreateTable(
                name: "NotificationAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationAttachments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAttachments_ApplicationId_CreatedAt",
                table: "NotificationAttachments",
                columns: new[] { "ApplicationId", "CreatedAt" });
        }
    }
}
