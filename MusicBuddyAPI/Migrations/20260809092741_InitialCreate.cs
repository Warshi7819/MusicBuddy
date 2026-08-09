using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicBuddyAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Themes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    BodyBg = table.Column<string>(type: "TEXT", nullable: false),
                    BodyColor = table.Column<string>(type: "TEXT", nullable: false),
                    CardBg = table.Column<string>(type: "TEXT", nullable: false),
                    CardBorderColor = table.Column<string>(type: "TEXT", nullable: false),
                    PrimaryColor = table.Column<string>(type: "TEXT", nullable: false),
                    NavbarBg = table.Column<string>(type: "TEXT", nullable: false),
                    NavbarTextColor = table.Column<string>(type: "TEXT", nullable: false),
                    FooterBg = table.Column<string>(type: "TEXT", nullable: false),
                    MutedColor = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDisabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Themes");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
