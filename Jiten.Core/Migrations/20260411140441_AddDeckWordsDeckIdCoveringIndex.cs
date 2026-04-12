using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDeckWordsDeckIdCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DeckWords_DeckId_IncWordIdReadingIndexOcc"
                                 ON "jiten"."DeckWords" ("DeckId")
                                 INCLUDE ("WordId", "ReadingIndex", "Occurrences");
                                 """, suppressTransaction: true);

            migrationBuilder.Sql("""
                                 DROP INDEX IF EXISTS "jiten"."IX_DeckId";
                                 """, suppressTransaction: true);

            migrationBuilder.Sql("""
                                 DROP INDEX IF EXISTS "jiten"."IX_WordReadingIndex";
                                 """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 DROP INDEX IF EXISTS "jiten"."IX_DeckWords_DeckId_IncWordIdReadingIndexOcc";
                                 """, suppressTransaction: true);

            migrationBuilder.Sql("""
                                 CREATE INDEX IF NOT EXISTS "IX_DeckId"
                                 ON "jiten"."DeckWords" ("DeckId");
                                 """, suppressTransaction: true);

            migrationBuilder.Sql("""
                                 CREATE INDEX IF NOT EXISTS "IX_WordReadingIndex"
                                 ON "jiten"."DeckWords" ("WordId", "ReadingIndex");
                                 """, suppressTransaction: true);
        }
    }
}
