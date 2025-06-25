using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class ExampleSentencesIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExampleSentenceWord_WordId",
                schema: "jiten",
                table: "ExampleSentenceWords");

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentenceWord_WordIdReadingIndex",
                schema: "jiten",
                table: "ExampleSentenceWords",
                columns: new[] { "WordId", "ReadingIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExampleSentenceWord_WordIdReadingIndex",
                schema: "jiten",
                table: "ExampleSentenceWords");

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentenceWord_WordId",
                schema: "jiten",
                table: "ExampleSentenceWords",
                column: "WordId");
        }
    }
}
