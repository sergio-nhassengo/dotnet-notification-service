using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUsersAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite schemas come from SimplifyNotificationPersistence's raw SQL, which created these
            // tables with IF NOT EXISTS; drop them the same way so older files without them still upgrade.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("""
                    DROP TABLE IF EXISTS "Users";
                    DROP TABLE IF EXISTS "Roles";
                    """);
                return;
            }

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                // Recreates the empty tables as SimplifyNotificationPersistence defined them. Dropped user rows are not restored.
                migrationBuilder.Sql("""
                    CREATE TABLE "Roles" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_Roles" PRIMARY KEY AUTOINCREMENT,
                        "Name" TEXT NOT NULL,
                        "Description" TEXT NULL,
                        "Created" INTEGER NOT NULL,
                        "CreatedBy" TEXT NULL,
                        "LastModified" INTEGER NOT NULL,
                        "LastModifiedBy" TEXT NULL
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
                    CREATE UNIQUE INDEX "IX_Roles_Name" ON "Roles" ("Name");
                    CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
                    CREATE INDEX "IX_Users_RoleId" ON "Users" ("RoleId");
                    CREATE UNIQUE INDEX "IX_Users_UserName" ON "Users" ("UserName");
                    """);
                return;
            }

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastLogin = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MobilePhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Created", "CreatedBy", "Description", "LastModified", "LastModifiedBy", "Name" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Full administrative access.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Admin" },
                    { 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Standard authenticated user.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "User" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);
        }
    }
}
