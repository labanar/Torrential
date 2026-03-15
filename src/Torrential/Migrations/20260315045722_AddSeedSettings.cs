using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torrential.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DesiredSeedTimeDays",
                table: "Torrents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DateFirstSeeded",
                table: "Torrents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeedSettings_DesiredSeedTimeDays",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.Sql("UPDATE Settings SET SeedSettings_DesiredSeedTimeDays = 14 WHERE SeedSettings_DesiredSeedTimeDays = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesiredSeedTimeDays",
                table: "Torrents");

            migrationBuilder.DropColumn(
                name: "DateFirstSeeded",
                table: "Torrents");

            migrationBuilder.DropColumn(
                name: "SeedSettings_DesiredSeedTimeDays",
                table: "Settings");
        }
    }
}
