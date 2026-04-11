using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDeckRawTextPgroongaIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DeckRawTexts_RawText_pgroonga"
                ON jiten."DeckRawTexts"
                USING pgroonga ("RawText")
                WITH (tokenizer='TokenBigramSplitSymbolAlphaDigit');
            """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS jiten."IX_DeckRawTexts_RawText_pgroonga";
            """, suppressTransaction: true);
        }
    }
}
