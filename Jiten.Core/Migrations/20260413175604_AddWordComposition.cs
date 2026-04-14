using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddWordComposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WordCompositions",
                schema: "jmdict",
                columns: table => new
                {
                    WordId = table.Column<int>(type: "integer", nullable: false),
                    ReadingIndex = table.Column<short>(type: "smallint", nullable: false),
                    Position = table.Column<short>(type: "smallint", nullable: false),
                    ComponentWordId = table.Column<int>(type: "integer", nullable: false),
                    ComponentReadingIndex = table.Column<short>(type: "smallint", nullable: false),
                    ComponentSurface = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordCompositions", x => new { x.WordId, x.ReadingIndex, x.Position });
                    table.ForeignKey(
                        name: "FK_WordCompositions_Words_ComponentWordId",
                        column: x => x.ComponentWordId,
                        principalSchema: "jmdict",
                        principalTable: "Words",
                        principalColumn: "WordId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WordCompositions_Words_WordId",
                        column: x => x.WordId,
                        principalSchema: "jmdict",
                        principalTable: "Words",
                        principalColumn: "WordId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WordComposition_ComponentWordId",
                schema: "jmdict",
                table: "WordCompositions",
                column: "ComponentWordId");

            migrationBuilder.CreateIndex(
                name: "IX_WordComposition_WordId_ReadingIndex",
                schema: "jmdict",
                table: "WordCompositions",
                columns: new[] { "WordId", "ReadingIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WordCompositions",
                schema: "jmdict");
        }
    }
}
