using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torrential.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Torrents",
                columns: table => new
                {
                    InfoHash = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TotalSize = table.Column<long>(type: "INTEGER", nullable: false),
                    PieceSize = table.Column<long>(type: "INTEGER", nullable: false),
                    NumberOfPieces = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesJson = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedFileIndicesJson = table.Column<string>(type: "TEXT", nullable: false),
                    AnnounceUrlsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PieceHashes = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    DateAdded = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Torrents", x => x.InfoHash);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Torrents");
        }
    }
}
