using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDifficultyVoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EasierVoteCount",
                schema: "jiten",
                table: "DeckDifficulty",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HarderVoteCount",
                schema: "jiten",
                table: "DeckDifficulty",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "UserAdjustment",
                schema: "jiten",
                table: "DeckDifficulty",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DifficultyRatings",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DifficultyRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DifficultyRatings_Decks_DeckId",
                        column: x => x.DeckId,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DifficultyVotes",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DeckLowId = table.Column<int>(type: "integer", nullable: false),
                    DeckHighId = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DifficultyVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DifficultyVotes_Decks_DeckHighId",
                        column: x => x.DeckHighId,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DifficultyVotes_Decks_DeckLowId",
                        column: x => x.DeckLowId,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkippedComparisons",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DeckLowId = table.Column<int>(type: "integer", nullable: false),
                    DeckHighId = table.Column<int>(type: "integer", nullable: false),
                    Permanent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkippedComparisons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyRatings_DeckId",
                schema: "jiten",
                table: "DifficultyRatings",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyRatings_UserDeck",
                schema: "jiten",
                table: "DifficultyRatings",
                columns: new[] { "UserId", "DeckId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyVotes_DeckHighId",
                schema: "jiten",
                table: "DifficultyVotes",
                column: "DeckHighId");

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyVotes_DeckLowId",
                schema: "jiten",
                table: "DifficultyVotes",
                column: "DeckLowId");

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyVotes_Unique",
                schema: "jiten",
                table: "DifficultyVotes",
                columns: new[] { "UserId", "DeckLowId", "DeckHighId" },
                unique: true,
                filter: "\"IsValid\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyVotes_UserId",
                schema: "jiten",
                table: "DifficultyVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SkippedComparisons_UserPair",
                schema: "jiten",
                table: "SkippedComparisons",
                columns: new[] { "UserId", "DeckLowId", "DeckHighId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DifficultyRatings",
                schema: "jiten");

            migrationBuilder.DropTable(
                name: "DifficultyVotes",
                schema: "jiten");

            migrationBuilder.DropTable(
                name: "SkippedComparisons",
                schema: "jiten");

            migrationBuilder.DropColumn(
                name: "EasierVoteCount",
                schema: "jiten",
                table: "DeckDifficulty");

            migrationBuilder.DropColumn(
                name: "HarderVoteCount",
                schema: "jiten",
                table: "DeckDifficulty");

            migrationBuilder.DropColumn(
                name: "UserAdjustment",
                schema: "jiten",
                table: "DeckDifficulty");
        }
    }
}
