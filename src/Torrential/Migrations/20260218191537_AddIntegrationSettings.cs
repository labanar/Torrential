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
            migrationBuilder.AddColumn<bool>(
                name: "IntegrationSettings_SlackEnabled",
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

            migrationBuilder.AddColumn<bool>(
                name: "IntegrationSettings_DiscordEnabled",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrationSettings_SlackEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_SlackWebhookUrl",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_DiscordEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IntegrationSettings_DiscordWebhookUrl",
                table: "Settings");
        }
    }
}
