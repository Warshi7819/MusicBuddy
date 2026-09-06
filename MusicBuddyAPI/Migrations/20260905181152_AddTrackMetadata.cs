using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicBuddyAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    UrlPath = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    TrackName = table.Column<string>(type: "TEXT", nullable: true),
                    Artist = table.Column<string>(type: "TEXT", nullable: true),
                    AlbumArtist = table.Column<string>(type: "TEXT", nullable: true),
                    Album = table.Column<string>(type: "TEXT", nullable: true),
                    Genre = table.Column<string>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ChannelCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtistFolder = table.Column<string>(type: "TEXT", nullable: false),
                    LastScanned = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackMetadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackMetadata_Artist",
                table: "TrackMetadata",
                column: "Artist");

            migrationBuilder.CreateIndex(
                name: "IX_TrackMetadata_FilePath",
                table: "TrackMetadata",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackMetadata_Genre",
                table: "TrackMetadata",
                column: "Genre");

            migrationBuilder.CreateIndex(
                name: "IX_TrackMetadata_TrackName",
                table: "TrackMetadata",
                column: "TrackName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackMetadata");
        }
    }
}
