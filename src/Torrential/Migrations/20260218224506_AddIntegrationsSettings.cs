using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torrential.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IntegrationsSettings_CommandHookEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationsSettings_CommandTemplate",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationsSettings_CommandTriggerDownloadComplete",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationsSettings_CommandWorkingDirectory",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationsSettings_DiscordEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationsSettings_DiscordMessageTemplate",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "Download completed: {name}");

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationsSettings_DiscordTriggerDownloadComplete",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationsSettings_DiscordWebhookUrl",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationsSettings_SlackEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationsSettings_SlackMessageTemplate",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "Download completed: {name}");

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationsSettings_SlackTriggerDownloadComplete",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationsSettings_SlackWebhookUrl",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_CommandHookEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_CommandTemplate",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_CommandTriggerDownloadComplete",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_CommandWorkingDirectory",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_DiscordEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_DiscordMessageTemplate",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_DiscordTriggerDownloadComplete",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_DiscordWebhookUrl",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_SlackEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_SlackMessageTemplate",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_SlackTriggerDownloadComplete",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationsSettings_SlackWebhookUrl",
                table: "Settings");
        }
    }
}
