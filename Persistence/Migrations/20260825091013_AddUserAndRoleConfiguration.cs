using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndRoleConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Created", "LastModified" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 8, 25, 9, 10, 13, 315, DateTimeKind.Unspecified).AddTicks(8240), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 8, 25, 9, 10, 13, 315, DateTimeKind.Unspecified).AddTicks(8240), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Created", "LastModified" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 8, 25, 9, 10, 13, 315, DateTimeKind.Unspecified).AddTicks(8620), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 8, 25, 9, 10, 13, 315, DateTimeKind.Unspecified).AddTicks(8620), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Created", "LastModified" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 8, 25, 8, 45, 37, 558, DateTimeKind.Unspecified).AddTicks(6470), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 8, 25, 8, 45, 37, 558, DateTimeKind.Unspecified).AddTicks(6480), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Created", "LastModified" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 8, 25, 8, 45, 37, 558, DateTimeKind.Unspecified).AddTicks(7590), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 8, 25, 8, 45, 37, 558, DateTimeKind.Unspecified).AddTicks(7590), new TimeSpan(0, 0, 0, 0, 0)) });
        }
    }
}
