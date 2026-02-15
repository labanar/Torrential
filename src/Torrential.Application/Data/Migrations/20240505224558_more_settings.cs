using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torrential.Application.Data.Migrations
{
    /// <inheritdoc />
    public partial class more_settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultTorrentSettings_MaxConnections",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GlobalTorrentSettings_MaxConnections",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TcpListenerSettings_Enabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TcpListenerSettings_Port",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultTorrentSettings_MaxConnections",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "GlobalTorrentSettings_MaxConnections",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TcpListenerSettings_Enabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TcpListenerSettings_Port",
                table: "Settings");
        }
    }
}
