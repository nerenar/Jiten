using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class Aliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeckTitles",
                schema: "jiten",
                columns: table => new
                {
                    DeckTitleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitleType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckTitles", x => x.DeckTitleId);
                    table.ForeignKey(
                        name: "FK_DeckTitles_Decks_DeckId",
                        column: x => x.DeckId,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeckTitles_DeckId_TitleType",
                schema: "jiten",
                table: "DeckTitles",
                columns: new[] { "DeckId", "TitleType" });

            migrationBuilder.CreateIndex(
                name: "IX_DeckTitles_Title",
                schema: "jiten",
                table: "DeckTitles",
                column: "Title");
            
            migrationBuilder.Sql("""
                INSERT INTO jiten."DeckTitles" ("DeckId", "Title", "TitleType")
                SELECT "DeckId", "OriginalTitle", 0
                FROM jiten."Decks"
                WHERE "OriginalTitle" IS NOT NULL;
            """);

            migrationBuilder.Sql("""
                INSERT INTO jiten."DeckTitles" ("DeckId", "Title", "TitleType")
                SELECT "DeckId", "RomajiTitle", 1
                FROM jiten."Decks"
                WHERE "RomajiTitle" IS NOT NULL;
            """);

            migrationBuilder.Sql("""
                INSERT INTO jiten."DeckTitles" ("DeckId", "Title", "TitleType")
                SELECT "DeckId", "EnglishTitle", 2
                FROM jiten."Decks"
                WHERE "EnglishTitle" IS NOT NULL;
            """);

            // 4) Create PGroonga indexes
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_DeckTitles_Title_pgroonga"
                ON jiten."DeckTitles"
                USING pgroonga ("Title");
            """);

            migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_DeckTitles_Title_NoSpace_pgroonga"
            ON jiten."DeckTitles"
            USING pgroonga ((regexp_replace("Title", E'\\s+', '', 'g')));
            """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION jiten.sync_decktitles_from_decks()
                RETURNS trigger AS $$
                BEGIN
                  -- Remove existing main title rows
                  DELETE FROM jiten."DeckTitles"
                    WHERE "DeckId" = NEW."DeckId"
                      AND "TitleType" IN (0,1,2);

                  -- Reinsert new values if not null
                  IF NEW."OriginalTitle" IS NOT NULL THEN
                    INSERT INTO jiten."DeckTitles" ("DeckId","Title","TitleType")
                    VALUES (NEW."DeckId", NEW."OriginalTitle", 0);
                  END IF;

                  IF NEW."RomajiTitle" IS NOT NULL THEN
                    INSERT INTO jiten."DeckTitles" ("DeckId","Title","TitleType")
                    VALUES (NEW."DeckId", NEW."RomajiTitle", 1);
                  END IF;

                  IF NEW."EnglishTitle" IS NOT NULL THEN
                    INSERT INTO jiten."DeckTitles" ("DeckId","Title","TitleType")
                    VALUES (NEW."DeckId", NEW."EnglishTitle", 2);
                  END IF;

                  RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS jiten_trg_sync_decktitles_after_ins_upd ON jiten."Decks";

                CREATE TRIGGER jiten_trg_sync_decktitles_after_ins_upd
                AFTER INSERT OR UPDATE ON jiten."Decks"
                FOR EACH ROW
                EXECUTE FUNCTION jiten.sync_decktitles_from_decks();
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                     DROP TRIGGER IF EXISTS jiten_trg_sync_decktitles_after_ins_upd ON jiten."Decks";
                                     DROP FUNCTION IF EXISTS jiten.sync_decktitles_from_decks();
                                 """);
            
            migrationBuilder.DropTable(
                name: "DeckTitles",
                schema: "jiten");
        }
    }
}
