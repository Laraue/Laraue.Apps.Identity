using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Laraue.Apps.Identity.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMergedInto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "merged_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merged_into_user_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_merged_into_user_id",
                table: "users",
                column: "merged_into_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_users_merged_into_user_id",
                table: "users",
                column: "merged_into_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_users_merged_into_user_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_merged_into_user_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "merged_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "merged_into_user_id",
                table: "users");
        }
    }
}
