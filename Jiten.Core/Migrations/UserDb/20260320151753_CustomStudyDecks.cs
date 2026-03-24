using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class CustomStudyDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserStudyDecks_UserId_DeckId",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.AlterColumn<int>(
                name: "DeckId",
                schema: "user",
                table: "UserStudyDecks",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "DeckType",
                schema: "user",
                table: "UserStudyDecks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "user",
                table: "UserStudyDecks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxGlobalFrequency",
                schema: "user",
                table: "UserStudyDecks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinGlobalFrequency",
                schema: "user",
                table: "UserStudyDecks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "user",
                table: "UserStudyDecks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PosFilter",
                schema: "user",
                table: "UserStudyDecks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserStudyDeckWords",
                schema: "user",
                columns: table => new
                {
                    UserStudyDeckId = table.Column<int>(type: "integer", nullable: false),
                    WordId = table.Column<int>(type: "integer", nullable: false),
                    ReadingIndex = table.Column<short>(type: "smallint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Occurrences = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStudyDeckWords", x => new { x.UserStudyDeckId, x.WordId, x.ReadingIndex });
                    table.ForeignKey(
                        name: "FK_UserStudyDeckWords_UserStudyDecks_UserStudyDeckId",
                        column: x => x.UserStudyDeckId,
                        principalSchema: "user",
                        principalTable: "UserStudyDecks",
                        principalColumn: "UserStudyDeckId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserStudyDecks_UserId_DeckId",
                schema: "user",
                table: "UserStudyDecks",
                columns: new[] { "UserId", "DeckId" },
                unique: true,
                filter: "\"DeckId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserStudyDeckWords_UserStudyDeckId_Occurrences",
                schema: "user",
                table: "UserStudyDeckWords",
                columns: new[] { "UserStudyDeckId", "Occurrences" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserStudyDeckWords_UserStudyDeckId_SortOrder",
                schema: "user",
                table: "UserStudyDeckWords",
                columns: new[] { "UserStudyDeckId", "SortOrder" });

            migrationBuilder.DropColumn(
                name: "ExcludeMatureMasteredBlacklisted",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.DropColumn(
                name: "ExcludeAllTrackedWords",
                schema: "user",
                table: "UserStudyDecks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludeMatureMasteredBlacklisted",
                schema: "user",
                table: "UserStudyDecks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeAllTrackedWords",
                schema: "user",
                table: "UserStudyDecks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropTable(
                name: "UserStudyDeckWords",
                schema: "user");

            migrationBuilder.DropIndex(
                name: "IX_UserStudyDecks_UserId_DeckId",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.DropColumn(
                name: "DeckType",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.DropColumn(
                name: "MaxGlobalFrequency",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.DropColumn(
                name: "MinGlobalFrequency",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.DropColumn(
                name: "PosFilter",
                schema: "user",
                table: "UserStudyDecks");

            migrationBuilder.AlterColumn<int>(
                name: "DeckId",
                schema: "user",
                table: "UserStudyDecks",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserStudyDecks_UserId_DeckId",
                schema: "user",
                table: "UserStudyDecks",
                columns: new[] { "UserId", "DeckId" },
                unique: true);
        }
    }
}
