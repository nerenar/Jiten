using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class JmDictV2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Definitions_WordId",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.AddColumn<List<string>>(
                name: "Dial",
                schema: "jmdict",
                table: "Definitions",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "Field",
                schema: "jmdict",
                table: "Definitions",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<bool>(
                name: "IsActiveInLatestSource",
                schema: "jmdict",
                table: "Definitions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Misc",
                schema: "jmdict",
                table: "Definitions",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "Pos",
                schema: "jmdict",
                table: "Definitions",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<short>>(
                name: "RestrictedToReadingIndices",
                schema: "jmdict",
                table: "Definitions",
                type: "smallint[]",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SenseIndex",
                schema: "jmdict",
                table: "Definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WordFormFrequencies",
                schema: "jmdict",
                columns: table => new
                {
                    WordId = table.Column<int>(type: "integer", nullable: false),
                    ReadingIndex = table.Column<short>(type: "smallint", nullable: false),
                    FrequencyRank = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FrequencyPercentage = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    ObservedFrequency = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    UsedInMediaAmount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordFormFrequencies", x => new { x.WordId, x.ReadingIndex });
                });

            migrationBuilder.CreateTable(
                name: "WordForms",
                schema: "jmdict",
                columns: table => new
                {
                    WordId = table.Column<int>(type: "integer", nullable: false),
                    ReadingIndex = table.Column<short>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    RubyText = table.Column<string>(type: "text", nullable: false),
                    FormType = table.Column<short>(type: "smallint", nullable: false),
                    IsActiveInLatestSource = table.Column<bool>(type: "boolean", nullable: false),
                    Priorities = table.Column<List<string>>(type: "text[]", nullable: true),
                    InfoTags = table.Column<List<string>>(type: "text[]", nullable: true),
                    IsObsolete = table.Column<bool>(type: "boolean", nullable: false),
                    IsNoKanji = table.Column<bool>(type: "boolean", nullable: false),
                    IsSearchOnly = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordForms", x => new { x.WordId, x.ReadingIndex });
                    table.ForeignKey(
                        name: "FK_WordForms_Words_WordId",
                        column: x => x.WordId,
                        principalSchema: "jmdict",
                        principalTable: "Words",
                        principalColumn: "WordId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Definitions_WordId_SenseIndex",
                schema: "jmdict",
                table: "Definitions",
                columns: new[] { "WordId", "SenseIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_WordFormFrequencies_FrequencyRank",
                schema: "jmdict",
                table: "WordFormFrequencies",
                column: "FrequencyRank");

            migrationBuilder.CreateIndex(
                name: "IX_WordForms_WordId",
                schema: "jmdict",
                table: "WordForms",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_WordForms_WordId_FormType_Text",
                schema: "jmdict",
                table: "WordForms",
                columns: new[] { "WordId", "FormType", "Text" },
                unique: true);

            // Backfill WordForms from Words arrays
            migrationBuilder.Sql(@"
INSERT INTO jmdict.""WordForms"" (""WordId"",""ReadingIndex"",""Text"",""RubyText"",""FormType"",
  ""IsActiveInLatestSource"",""IsObsolete"",""IsNoKanji"",""IsSearchOnly"")
SELECT w.""WordId"", (r.ordinality-1)::smallint, r.reading, f.furigana, t.type::smallint,
  true, false, false, false
FROM jmdict.""Words"" w
CROSS JOIN LATERAL unnest(w.""Readings"") WITH ORDINALITY AS r(reading, ordinality)
CROSS JOIN LATERAL unnest(w.""ReadingsFurigana"") WITH ORDINALITY AS f(furigana, ordinality)
CROSS JOIN LATERAL unnest(w.""ReadingTypes"") WITH ORDINALITY AS t(type, ordinality)
WHERE r.ordinality = f.ordinality AND r.ordinality = t.ordinality;
");

            // Backfill SenseIndex on Definitions
            migrationBuilder.Sql(@"
WITH ranked AS (
  SELECT ""DefinitionId"",
    ROW_NUMBER() OVER (PARTITION BY ""WordId"" ORDER BY ""DefinitionId"") - 1 AS sense_index
  FROM jmdict.""Definitions""
)
UPDATE jmdict.""Definitions"" d SET ""SenseIndex"" = ranked.sense_index
FROM ranked WHERE d.""DefinitionId"" = ranked.""DefinitionId"";
");

            // Bridge-fill Pos from existing PartsOfSpeech
            migrationBuilder.Sql(@"
UPDATE jmdict.""Definitions"" SET ""Pos"" = ""PartsOfSpeech"";
");

            // Backfill WordFormFrequencies from WordFrequencies arrays
            migrationBuilder.Sql(@"
INSERT INTO jmdict.""WordFormFrequencies"" (""WordId"",""ReadingIndex"",""FrequencyRank"",
  ""FrequencyPercentage"",""ObservedFrequency"",""UsedInMediaAmount"")
SELECT f.""WordId"", (r.ordinality-1)::smallint, r.rank, p.pct, o.obs, m.media
FROM jmdict.""WordFrequencies"" f
CROSS JOIN LATERAL unnest(f.""ReadingsFrequencyRank"") WITH ORDINALITY AS r(rank, ordinality)
CROSS JOIN LATERAL unnest(f.""ReadingsFrequencyPercentage"") WITH ORDINALITY AS p(pct, ordinality)
CROSS JOIN LATERAL unnest(f.""ReadingsObservedFrequency"") WITH ORDINALITY AS o(obs, ordinality)
CROSS JOIN LATERAL unnest(f.""ReadingsUsedInMediaAmount"") WITH ORDINALITY AS m(media, ordinality)
WHERE r.ordinality = p.ordinality AND r.ordinality = o.ordinality AND r.ordinality = m.ordinality;
");

            // Add index on Lookups.LookupKey
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_Lookups_LookupKey"" ON jmdict.""Lookups"" (""LookupKey"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS jmdict.""IX_Lookups_LookupKey"";");

            migrationBuilder.DropTable(
                name: "WordFormFrequencies",
                schema: "jmdict");

            migrationBuilder.DropTable(
                name: "WordForms",
                schema: "jmdict");

            migrationBuilder.DropIndex(
                name: "IX_Definitions_WordId_SenseIndex",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.DropColumn(
                name: "Dial",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.DropColumn(
                name: "Field",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.DropColumn(
                name: "IsActiveInLatestSource",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.DropColumn(
                name: "Misc",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.DropColumn(
                name: "Pos",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.DropColumn(
                name: "RestrictedToReadingIndices",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.DropColumn(
                name: "SenseIndex",
                schema: "jmdict",
                table: "Definitions");

            migrationBuilder.CreateIndex(
                name: "IX_Definitions_WordId",
                schema: "jmdict",
                table: "Definitions",
                column: "WordId");
        }
    }
}
