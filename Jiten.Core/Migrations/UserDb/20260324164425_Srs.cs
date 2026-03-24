using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class Srs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserKnownWords",
                schema: "user");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserKnownWords",
                schema: "user",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<int>(type: "integer", nullable: false),
                    ReadingIndex = table.Column<byte>(type: "smallint", nullable: false),
                    KnownState = table.Column<int>(type: "integer", nullable: false),
                    LearnedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserKnownWords", x => new { x.UserId, x.WordId, x.ReadingIndex });
                    table.ForeignKey(
                        name: "FK_UserKnownWords_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "user",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserKnownWord_UserId",
                schema: "user",
                table: "UserKnownWords",
                column: "UserId");
        }
    }
}
