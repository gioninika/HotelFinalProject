using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hotel.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "AppUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailCodeExpiresAtUtc",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmailCodeFailedAttempts",
                table: "AppUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmailCodeHash",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailCodePurpose",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailCodeSentAtUtc",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "AppUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Preserve the email address already held on each manager record.
            // Existing guests have no email in the old schema and must add one later.
            migrationBuilder.Sql("""
                UPDATE account
                SET Email = LOWER(LTRIM(RTRIM(manager.Email)))
                FROM AppUsers AS account
                INNER JOIN Managers AS manager ON manager.Id = account.ManagerId
                WHERE account.Email IS NULL;
                """);

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Email", "EmailCodeExpiresAtUtc", "EmailCodeFailedAttempts", "EmailCodeHash", "EmailCodePurpose", "EmailCodeSentAtUtc", "EmailConfirmed" },
                values: new object[] { "admin@hms.com", null, 0, null, null, null, true });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailCodeExpiresAtUtc",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailCodeFailedAttempts",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailCodeHash",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailCodePurpose",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailCodeSentAtUtc",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "AppUsers");
        }
    }
}
