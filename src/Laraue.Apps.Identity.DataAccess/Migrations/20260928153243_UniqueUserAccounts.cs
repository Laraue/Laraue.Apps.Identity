using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Laraue.Apps.Identity.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUserAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_telegram_accounts_user_id",
                table: "telegram_accounts");

            migrationBuilder.DropIndex(
                name: "ix_google_accounts_user_id",
                table: "google_accounts");

            migrationBuilder.CreateIndex(
                name: "ix_telegram_accounts_user_id",
                table: "telegram_accounts",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_google_accounts_user_id",
                table: "google_accounts",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_telegram_accounts_user_id",
                table: "telegram_accounts");

            migrationBuilder.DropIndex(
                name: "ix_google_accounts_user_id",
                table: "google_accounts");

            migrationBuilder.CreateIndex(
                name: "ix_telegram_accounts_user_id",
                table: "telegram_accounts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_google_accounts_user_id",
                table: "google_accounts",
                column: "user_id");
        }
    }
}
