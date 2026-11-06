using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations;

public partial class RemoveRedundantNotificationFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Historical notifications can predate ContentJson. Preserve their recipient before dropping it.
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            migrationBuilder.Sql("""
                UPDATE "EmailNotifications"
                SET "ContentJson" = json_object('To', json_array(json_object('Email', "RecipientEmail", 'Name', "RecipientName")),
                    'Cc', json_array(), 'Attachments', json_array(), 'HtmlBody', NULL, 'TextBody', NULL)
                WHERE "ContentJson" IS NULL;
                DROP INDEX "IX_EmailNotifications_ApplicationId_BusinessReference";
                ALTER TABLE "EmailNotifications" DROP COLUMN "RecipientEmail";
                ALTER TABLE "EmailNotifications" DROP COLUMN "RecipientName";
                ALTER TABLE "EmailNotifications" DROP COLUMN "BusinessReference";
                """);
            return;
        }

        migrationBuilder.Sql("""
            UPDATE [EmailNotifications]
            SET [ContentJson] = (SELECT JSON_QUERY((SELECT [RecipientEmail] AS [Email], [RecipientName] AS [Name]
                FOR JSON PATH, INCLUDE_NULL_VALUES)) AS [To], JSON_QUERY('[]') AS [Cc], JSON_QUERY('[]') AS [Attachments]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
            WHERE [ContentJson] IS NULL;
            """);
        migrationBuilder.DropIndex("IX_EmailNotifications_ApplicationId_BusinessReference", "EmailNotifications");
        migrationBuilder.DropColumn("RecipientEmail", "EmailNotifications");
        migrationBuilder.DropColumn("RecipientName", "EmailNotifications");
        migrationBuilder.DropColumn("BusinessReference", "EmailNotifications");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("RecipientEmail", "EmailNotifications", maxLength: 320, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("RecipientName", "EmailNotifications", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>("BusinessReference", "EmailNotifications", maxLength: 200, nullable: true);
        migrationBuilder.Sql(ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite"
            ? """
              UPDATE "EmailNotifications" SET "RecipientEmail" = COALESCE(json_extract("ContentJson", '$.To[0].Email'), ''),
                  "RecipientName" = json_extract("ContentJson", '$.To[0].Name');
              """
            : """
              UPDATE [EmailNotifications] SET [RecipientEmail] = COALESCE(JSON_VALUE([ContentJson], '$.To[0].Email'), ''),
                  [RecipientName] = JSON_VALUE([ContentJson], '$.To[0].Name');
              """);
        migrationBuilder.CreateIndex("IX_EmailNotifications_ApplicationId_BusinessReference", "EmailNotifications", new[] { "ApplicationId", "BusinessReference" });
    }
}
