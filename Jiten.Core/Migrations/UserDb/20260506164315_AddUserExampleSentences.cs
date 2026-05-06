using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class AddUserExampleSentences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserExampleSentences",
                schema: "user",
                columns: table => new
                {
                    UserExampleSentenceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<int>(type: "integer", nullable: false),
                    ReadingIndex = table.Column<byte>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Source = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SortOrder = table.Column<byte>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExampleSentences", x => x.UserExampleSentenceId);
                    table.ForeignKey(
                        name: "FK_UserExampleSentences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "user",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserExampleSentence_UserId_WordId_ReadingIndex",
                schema: "user",
                table: "UserExampleSentences",
                columns: new[] { "UserId", "WordId", "ReadingIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_UserExampleSentence_UserId_WordId_ReadingIndex_SortOrder",
                schema: "user",
                table: "UserExampleSentences",
                columns: new[] { "UserId", "WordId", "ReadingIndex", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserExampleSentences",
                schema: "user");
        }
    }
}
