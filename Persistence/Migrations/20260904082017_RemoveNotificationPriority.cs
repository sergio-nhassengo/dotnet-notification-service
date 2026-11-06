using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotificationPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These historical migrations were SQL Server-only. SQLite is bootstrapped
            // and upgraded from its former development schema by SimplifyNotificationPersistence.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite") return;

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "EmailNotifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // These historical migrations were SQL Server-only. SQLite is bootstrapped
            // and upgraded from its former development schema by SimplifyNotificationPersistence.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite") return;

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "EmailNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
