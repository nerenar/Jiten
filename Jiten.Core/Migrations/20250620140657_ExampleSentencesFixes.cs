using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class ExampleSentencesFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExampleSentences_Decks_DeckId1",
                schema: "jiten",
                table: "ExampleSentences");

            migrationBuilder.DropForeignKey(
                name: "FK_ExampleSentenceWords_ExampleSentences_ExampleSentenceSenten~",
                schema: "jiten",
                table: "ExampleSentenceWords");

            migrationBuilder.DropForeignKey(
                name: "FK_ExampleSentenceWords_Words_WordId1",
                schema: "jiten",
                table: "ExampleSentenceWords");

            migrationBuilder.DropIndex(
                name: "IX_ExampleSentenceWords_ExampleSentenceSentenceId",
                schema: "jiten",
                table: "ExampleSentenceWords");

            migrationBuilder.DropIndex(
                name: "IX_ExampleSentenceWords_WordId1",
                schema: "jiten",
                table: "ExampleSentenceWords");

            migrationBuilder.DropIndex(
                name: "IX_ExampleSentences_DeckId1",
                schema: "jiten",
                table: "ExampleSentences");

            migrationBuilder.DropColumn(
                name: "ExampleSentenceSentenceId",
                schema: "jiten",
                table: "ExampleSentenceWords");

            migrationBuilder.DropColumn(
                name: "WordId1",
                schema: "jiten",
                table: "ExampleSentenceWords");

            migrationBuilder.DropColumn(
                name: "DeckId1",
                schema: "jiten",
                table: "ExampleSentences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExampleSentenceSentenceId",
                schema: "jiten",
                table: "ExampleSentenceWords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WordId1",
                schema: "jiten",
                table: "ExampleSentenceWords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeckId1",
                schema: "jiten",
                table: "ExampleSentences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentenceWords_ExampleSentenceSentenceId",
                schema: "jiten",
                table: "ExampleSentenceWords",
                column: "ExampleSentenceSentenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentenceWords_WordId1",
                schema: "jiten",
                table: "ExampleSentenceWords",
                column: "WordId1");

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentences_DeckId1",
                schema: "jiten",
                table: "ExampleSentences",
                column: "DeckId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ExampleSentences_Decks_DeckId1",
                schema: "jiten",
                table: "ExampleSentences",
                column: "DeckId1",
                principalSchema: "jiten",
                principalTable: "Decks",
                principalColumn: "DeckId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExampleSentenceWords_ExampleSentences_ExampleSentenceSenten~",
                schema: "jiten",
                table: "ExampleSentenceWords",
                column: "ExampleSentenceSentenceId",
                principalSchema: "jiten",
                principalTable: "ExampleSentences",
                principalColumn: "SentenceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExampleSentenceWords_Words_WordId1",
                schema: "jiten",
                table: "ExampleSentenceWords",
                column: "WordId1",
                principalSchema: "jmdict",
                principalTable: "Words",
                principalColumn: "WordId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
