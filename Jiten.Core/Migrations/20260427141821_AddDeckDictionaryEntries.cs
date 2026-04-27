using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDeckDictionaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeckDictionaryEntries",
                schema: "jiten",
                columns: table => new
                {
                    DeckDictionaryEntryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    Surface = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckDictionaryEntries", x => x.DeckDictionaryEntryId);
                    table.ForeignKey(
                        name: "FK_DeckDictionaryEntries_Decks_DeckId",
                        column: x => x.DeckId,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeckDictionaryEntry_DeckId_Surface",
                schema: "jiten",
                table: "DeckDictionaryEntries",
                columns: new[] { "DeckId", "Surface" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeckDictionaryEntries",
                schema: "jiten");
        }
    }
}
