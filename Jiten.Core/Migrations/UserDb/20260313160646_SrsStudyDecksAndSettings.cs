using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class SrsStudyDecksAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettingsJson",
                schema: "user",
                table: "UserFsrsSettings",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "user",
                table: "FsrsCards",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() at time zone 'utc'");

            migrationBuilder.CreateTable(
                name: "UserStudyDecks",
                schema: "user",
                columns: table => new
                {
                    UserStudyDeckId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DownloadType = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    MinFrequency = table.Column<int>(type: "integer", nullable: false),
                    MaxFrequency = table.Column<int>(type: "integer", nullable: false),
                    TargetPercentage = table.Column<float>(type: "real", nullable: true),
                    MinOccurrences = table.Column<int>(type: "integer", nullable: true),
                    MaxOccurrences = table.Column<int>(type: "integer", nullable: true),
                    ExcludeKana = table.Column<bool>(type: "boolean", nullable: false),
                    ExcludeMatureMasteredBlacklisted = table.Column<bool>(type: "boolean", nullable: false),
                    ExcludeAllTrackedWords = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStudyDecks", x => x.UserStudyDeckId);
                    table.ForeignKey(
                        name: "FK_UserStudyDecks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "user",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserStudyDeck_UserId",
                schema: "user",
                table: "UserStudyDecks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserStudyDecks_UserId_DeckId",
                schema: "user",
                table: "UserStudyDecks",
                columns: new[] { "UserId", "DeckId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserStudyDecks",
                schema: "user");

            migrationBuilder.DropColumn(
                name: "SettingsJson",
                schema: "user",
                table: "UserFsrsSettings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "user",
                table: "FsrsCards");
        }
    }
}
