using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torrential.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GlobalTorrentSettings_MaxConnections",
                table: "Settings",
                newName: "ConnectionSettings_MaxHalfOpenConnections");

            migrationBuilder.RenameColumn(
                name: "DefaultTorrentSettings_MaxConnections",
                table: "Settings",
                newName: "ConnectionSettings_MaxConnectionsPerTorrent");

            migrationBuilder.AddColumn<int>(
                name: "ConnectionSettings_MaxConnectionsGlobal",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionSettings_MaxConnectionsGlobal",
                table: "Settings");

            migrationBuilder.RenameColumn(
                name: "ConnectionSettings_MaxHalfOpenConnections",
                table: "Settings",
                newName: "GlobalTorrentSettings_MaxConnections");

            migrationBuilder.RenameColumn(
                name: "ConnectionSettings_MaxConnectionsPerTorrent",
                table: "Settings",
                newName: "DefaultTorrentSettings_MaxConnections");
        }
    }
}
