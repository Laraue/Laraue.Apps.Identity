using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Laraue.Apps.Identity.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "users",
                type: "character varying(257)",
                maxLength: 257,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "family_name",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "given_name",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "initials",
                table: "users",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "user_name",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Fill existing users from the account they were created with - the earliest one, Telegram
            // on a tie - the same way new users are filled (UserIdentityService.CreateUser), Google names cut to
            // User.NameMaxLength.
            migrationBuilder.Sql("""
                UPDATE users u
                SET user_name = t.telegram_user_name,
                    given_name = t.telegram_first_name,
                    family_name = t.telegram_last_name
                FROM telegram_accounts t
                WHERE t.user_id = u.id
                  AND NOT EXISTS (
                      SELECT 1 FROM google_accounts g
                      WHERE g.user_id = u.id AND g.created_at < t.created_at);
                """);

            migrationBuilder.Sql("""
                UPDATE users u
                SET given_name = left(CASE
                        WHEN g.given_name IS NOT NULL OR g.family_name IS NOT NULL THEN g.given_name
                        ELSE COALESCE(g.name, split_part(g.email, '@', 1))
                    END, 128),
                    family_name = left(CASE
                        WHEN g.given_name IS NOT NULL OR g.family_name IS NOT NULL THEN g.family_name
                    END, 128)
                FROM google_accounts g
                WHERE g.user_id = u.id
                  AND NOT EXISTS (
                      SELECT 1 FROM telegram_accounts t
                      WHERE t.user_id = u.id AND t.created_at <= g.created_at);
                """);

            // Same rules as UserDisplayName.From.
            migrationBuilder.Sql("""
                UPDATE users
                SET display_name = CASE
                        WHEN COALESCE(btrim(user_name), '') <> '' THEN user_name
                        WHEN COALESCE(btrim(given_name), '') <> '' AND COALESCE(btrim(family_name), '') <> ''
                            THEN given_name || ' ' || family_name
                        WHEN COALESCE(btrim(given_name), '') <> '' THEN given_name
                        WHEN COALESCE(btrim(family_name), '') <> '' THEN family_name
                        ELSE 'Unknown'
                    END,
                    initials = upper(CASE
                        WHEN COALESCE(btrim(user_name), '') <> '' THEN left(user_name, 2)
                        WHEN COALESCE(btrim(given_name), '') <> '' AND COALESCE(btrim(family_name), '') <> ''
                            THEN left(given_name, 1) || left(family_name, 1)
                        WHEN COALESCE(btrim(given_name), '') <> '' THEN left(given_name, 2)
                        WHEN COALESCE(btrim(family_name), '') <> '' THEN left(family_name, 2)
                        ELSE 'UN'
                    END);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "family_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "given_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "initials",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_name",
                table: "users");
        }
    }
}
