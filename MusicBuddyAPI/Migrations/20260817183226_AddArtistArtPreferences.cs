using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicBuddyAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddArtistArtPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArtistArtPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtistPath = table.Column<string>(type: "TEXT", nullable: false),
                    AlbumPath = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistArtPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtistArtPreferences_UserId_ArtistPath",
                table: "ArtistArtPreferences",
                columns: new[] { "UserId", "ArtistPath" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtistArtPreferences");
        }
    }
}
