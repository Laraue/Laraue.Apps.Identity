using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Laraue.Apps.Identity.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "telegram_first_name",
                table: "telegram_accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_language_code",
                table: "telegram_accounts",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_last_name",
                table: "telegram_accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_user_name",
                table: "telegram_accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "telegram_first_name",
                table: "telegram_accounts");

            migrationBuilder.DropColumn(
                name: "telegram_language_code",
                table: "telegram_accounts");

            migrationBuilder.DropColumn(
                name: "telegram_last_name",
                table: "telegram_accounts");

            migrationBuilder.DropColumn(
                name: "telegram_user_name",
                table: "telegram_accounts");
        }
    }
}
