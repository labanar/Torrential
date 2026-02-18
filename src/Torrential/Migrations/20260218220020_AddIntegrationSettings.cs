using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torrential.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntegrationSettings_Command",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationSettings_CommandEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationSettings_CommandOnTorrentComplete",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationSettings_DiscordEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationSettings_DiscordOnTorrentComplete",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationSettings_DiscordWebhookUrl",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationSettings_SlackEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationSettings_SlackOnTorrentComplete",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationSettings_SlackWebhookUrl",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrationSettings_Command",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_CommandEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_CommandOnTorrentComplete",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_DiscordEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_DiscordOnTorrentComplete",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_DiscordWebhookUrl",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_SlackEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_SlackOnTorrentComplete",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_SlackWebhookUrl",
                table: "Settings");
        }
    }
}
