using System;
using Jiten.Core;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations
{
    [DbContext(typeof(JitenDbContext))]
    [Migration("20260328120000_AddDifficultyRankings")]
    /// <inheritdoc />
    public partial class AddDifficultyRankings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                schema: "jiten",
                table: "DifficultyVotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DifficultyRankGroups",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    MediaTypeGroup = table.Column<int>(type: "integer", nullable: false),
                    SortIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DifficultyRankGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DifficultyRankItems",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DifficultyRankItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DifficultyRankItems_Decks_DeckId",
                        column: x => x.DeckId,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DifficultyRankItems_DifficultyRankGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "jiten",
                        principalTable: "DifficultyRankGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyRankGroups_UserGroupSort",
                schema: "jiten",
                table: "DifficultyRankGroups",
                columns: new[] { "UserId", "MediaTypeGroup", "SortIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyRankItems_DeckId",
                schema: "jiten",
                table: "DifficultyRankItems",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyRankItems_GroupId",
                schema: "jiten",
                table: "DifficultyRankItems",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyRankItems_UserDeck",
                schema: "jiten",
                table: "DifficultyRankItems",
                columns: new[] { "UserId", "DeckId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DifficultyRankItems",
                schema: "jiten");

            migrationBuilder.DropTable(
                name: "DifficultyRankGroups",
                schema: "jiten");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "jiten",
                table: "DifficultyVotes");
        }
    }
}
