using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class UserCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCoverages",
                schema: "user",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    Coverage = table.Column<double>(type: "double precision", nullable: false),
                    UniqueCoverage = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCoverages", x => new { x.UserId, x.DeckId });
                });

            migrationBuilder.CreateTable(
                name: "UserKnownWords",
                schema: "user",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    WordId = table.Column<int>(type: "integer", nullable: false),
                    ReadingIndex = table.Column<int>(type: "integer", nullable: false),
                    LearnedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KnownState = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserKnownWords", x => new { x.UserId, x.WordId, x.ReadingIndex });
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCoverage_UserId",
                schema: "user",
                table: "UserCoverages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserKnownWord_UserId",
                schema: "user",
                table: "UserKnownWords",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCoverages",
                schema: "user");

            migrationBuilder.DropTable(
                name: "UserKnownWords",
                schema: "user");
        }
    }
}
