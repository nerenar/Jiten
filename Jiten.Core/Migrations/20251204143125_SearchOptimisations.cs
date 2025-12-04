using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class SearchOptimisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                     ALTER TABLE jiten."DeckTitles"
                                     ADD COLUMN IF NOT EXISTS "TitleNoSpaces" text
                                     GENERATED ALWAYS AS (REPLACE("Title", ' ', '')) STORED;
                                 """);

            migrationBuilder.Sql("""
                                     DROP INDEX IF EXISTS jiten."IX_DeckTitles_Title_NoSpace_pgroonga";
                                 """);

            migrationBuilder.Sql("""
                                     CREATE INDEX IF NOT EXISTS "IX_DeckTitles_TitleNoSpaces_pgroonga"
                                     ON jiten."DeckTitles"
                                     USING pgroonga ("TitleNoSpaces");
                                 """);

            migrationBuilder.Sql("""
                                     CREATE INDEX IF NOT EXISTS "IX_DeckTitles_Title_lower"
                                     ON jiten."DeckTitles" (LOWER("Title"));
                                 """);

            migrationBuilder.Sql("""
                                     CREATE INDEX IF NOT EXISTS "IX_DeckTitles_TitleNoSpaces_lower"
                                     ON jiten."DeckTitles" (LOWER("TitleNoSpaces"));
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                     DROP INDEX IF EXISTS jiten."IX_DeckTitles_TitleNoSpaces_lower";
                                 """);

            migrationBuilder.Sql("""
                                     DROP INDEX IF EXISTS jiten."IX_DeckTitles_Title_lower";
                                 """);

            migrationBuilder.Sql("""
                                     DROP INDEX IF EXISTS jiten."IX_DeckTitles_TitleNoSpaces_pgroonga";
                                 """);

            migrationBuilder.Sql("""
                                     CREATE INDEX IF NOT EXISTS "IX_DeckTitles_Title_NoSpace_pgroonga"
                                     ON jiten."DeckTitles"
                                     USING pgroonga ((regexp_replace("Title", E'\\s+', '', 'g')));
                                 """);

            migrationBuilder.Sql("""
                                     ALTER TABLE jiten."DeckTitles"
                                     DROP COLUMN IF EXISTS "TitleNoSpaces";
                                 """);
        }
    }
}
