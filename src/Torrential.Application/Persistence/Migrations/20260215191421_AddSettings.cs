using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torrential.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DownloadFolder = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedFolder = table.Column<string>(type: "TEXT", nullable: false),
                    MaxHalfOpenConnections = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPeersPerTorrent = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
