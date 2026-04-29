using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSentenceDifficulty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "Difficulty",
                schema: "jiten",
                table: "ExampleSentences",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentence_DeckId_Difficulty",
                schema: "jiten",
                table: "ExampleSentences",
                columns: new[] { "DeckId", "Difficulty" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExampleSentence_DeckId_Difficulty",
                schema: "jiten",
                table: "ExampleSentences");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                schema: "jiten",
                table: "ExampleSentences");
        }
    }
}
