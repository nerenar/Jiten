using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class ExampleSentences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float>(
                name: "Difficulty",
                schema: "jiten",
                table: "Decks",
                type: "real",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "ExampleSentences",
                schema: "jiten",
                columns: table => new
                {
                    SentenceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DeckId1 = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExampleSentences", x => x.SentenceId);
                    table.ForeignKey(
                        name: "FK_ExampleSentences_Decks_DeckId",
                        column: x => x.DeckId,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExampleSentences_Decks_DeckId1",
                        column: x => x.DeckId1,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExampleSentenceWords",
                schema: "jiten",
                columns: table => new
                {
                    ExampleSentenceId = table.Column<int>(type: "integer", nullable: false),
                    WordId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<byte>(type: "smallint", nullable: false),
                    Length = table.Column<byte>(type: "smallint", nullable: false),
                    ExampleSentenceSentenceId = table.Column<int>(type: "integer", nullable: false),
                    WordId1 = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExampleSentenceWords", x => new { x.ExampleSentenceId, x.WordId, x.Position });
                    table.ForeignKey(
                        name: "FK_ExampleSentenceWords_ExampleSentences_ExampleSentenceId",
                        column: x => x.ExampleSentenceId,
                        principalSchema: "jiten",
                        principalTable: "ExampleSentences",
                        principalColumn: "SentenceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExampleSentenceWords_ExampleSentences_ExampleSentenceSenten~",
                        column: x => x.ExampleSentenceSentenceId,
                        principalSchema: "jiten",
                        principalTable: "ExampleSentences",
                        principalColumn: "SentenceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExampleSentenceWords_Words_WordId",
                        column: x => x.WordId,
                        principalSchema: "jmdict",
                        principalTable: "Words",
                        principalColumn: "WordId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExampleSentenceWords_Words_WordId1",
                        column: x => x.WordId1,
                        principalSchema: "jmdict",
                        principalTable: "Words",
                        principalColumn: "WordId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentence_DeckId",
                schema: "jiten",
                table: "ExampleSentences",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentences_DeckId1",
                schema: "jiten",
                table: "ExampleSentences",
                column: "DeckId1");

            migrationBuilder.CreateIndex(
                name: "IX_ExampleSentenceWord_WordId",
                schema: "jiten",
                table: "ExampleSentenceWords",
                column: "WordId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExampleSentenceWords",
                schema: "jiten");

            migrationBuilder.DropTable(
                name: "ExampleSentences",
                schema: "jiten");

            migrationBuilder.AlterColumn<int>(
                name: "Difficulty",
                schema: "jiten",
                table: "Decks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real");
        }
    }
}
