using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationApplicationIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These historical migrations were SQL Server-only. SQLite is bootstrapped
            // and upgraded from its former development schema by SimplifyNotificationPersistence.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite") return;

            migrationBuilder.DropIndex(
                name: "IX_EmailNotifications_IdempotencyKey",
                table: "EmailNotifications");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationId",
                table: "EmailNotifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<string>(
                name: "BusinessReference",
                table: "EmailNotifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentJson",
                table: "EmailNotifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "EmailNotifications",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationAttachments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_ApplicationId_BusinessReference",
                table: "EmailNotifications",
                columns: new[] { "ApplicationId", "BusinessReference" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_ApplicationId_IdempotencyKey",
                table: "EmailNotifications",
                columns: new[] { "ApplicationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAttachments_ApplicationId_CreatedAt",
                table: "NotificationAttachments",
                columns: new[] { "ApplicationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // These historical migrations were SQL Server-only. SQLite is bootstrapped
            // and upgraded from its former development schema by SimplifyNotificationPersistence.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite") return;

            migrationBuilder.DropTable(
                name: "NotificationAttachments");

            migrationBuilder.DropIndex(
                name: "IX_EmailNotifications_ApplicationId_BusinessReference",
                table: "EmailNotifications");

            migrationBuilder.DropIndex(
                name: "IX_EmailNotifications_ApplicationId_IdempotencyKey",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "BusinessReference",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "ContentJson",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "EmailNotifications");

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_IdempotencyKey",
                table: "EmailNotifications",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}
