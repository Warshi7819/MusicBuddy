using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicBuddyAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChannelCount",
                table: "PlaylistTracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelCount",
                table: "PlaylistTracks");
        }
    }
}
