using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class Fsrs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                                         name: "FsrsCards",
                                         schema: "user",
                                         columns: table => new
                                                           {
                                                               CardId = table.Column<long>(type: "bigint", nullable: false)
                                                                             .Annotation("Npgsql:ValueGenerationStrategy",
                                                                                         NpgsqlValueGenerationStrategy
                                                                                             .IdentityByDefaultColumn),
                                                               UserId = table.Column<string>(type: "text", nullable: false),
                                                               WordId = table.Column<int>(type: "integer", nullable: false),
                                                               ReadingIndex = table.Column<byte>(type: "smallint", nullable: false),
                                                               State = table.Column<int>(type: "integer", nullable: false),
                                                               Step = table.Column<int>(type: "smallint", nullable: true),
                                                               Stability = table.Column<double>(type: "real", nullable: true),
                                                               Difficulty = table.Column<double>(type: "real", nullable: true),
                                                               Due = table.Column<DateTime>(type: "timestamp with time zone",
                                                                                            nullable: false),
                                                               LastReview = table.Column<DateTime>(type: "timestamp with time zone",
                                                                   nullable: true)
                                                           },
                                         constraints: table => { table.PrimaryKey("PK_FsrsCards", x => x.CardId); });

            migrationBuilder.CreateTable(
                                         name: "FsrsReviewLogs",
                                         schema: "user",
                                         columns: table => new
                                                           {
                                                               ReviewLogId = table.Column<long>(type: "bigint", nullable: false)
                                                                                  .Annotation("Npgsql:ValueGenerationStrategy",
                                                                                      NpgsqlValueGenerationStrategy
                                                                                          .IdentityByDefaultColumn),
                                                               CardId = table.Column<long>(type: "bigint", nullable: false),
                                                               ReviewDateTime =
                                                                   table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                                                           },
                                         constraints: table =>
                                         {
                                             table.PrimaryKey("PK_FsrsReviewLogs", x => x.ReviewLogId);
                                             table.ForeignKey(
                                                              name: "FK_FsrsReviewLog_FsrsCard_CardId",
                                                              column: x => x.CardId,
                                                              principalSchema: "user",
                                                              principalTable: "FsrsCards",
                                                              principalColumn: "CardId",
                                                              onDelete: ReferentialAction.Cascade);
                                         });

            migrationBuilder.CreateIndex(
                                         name: "IX_FsrsCards_UserId",
                                         schema: "user",
                                         table: "FsrsCards",
                                         column: "UserId");

            migrationBuilder.CreateIndex(
                                         name: "IX_FsrsCards_UserId_WordId_ReadingIndex",
                                         schema: "user",
                                         table: "FsrsCards",
                                         columns: new[] { "UserId", "WordId", "ReadingIndex" },
                                         unique: true);

            migrationBuilder.CreateIndex(
                                         name: "IX_FsrsReviewLogs_CardId_ReviewDateTime",
                                         schema: "user",
                                         table: "FsrsReviewLogs",
                                         columns: new[] { "CardId", "ReviewDateTime" },
                                         unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                                       name: "FsrsReviewLogs",
                                       schema: "user");

            migrationBuilder.DropTable(
                                       name: "FsrsCards",
                                       schema: "user");
        }
    }
}