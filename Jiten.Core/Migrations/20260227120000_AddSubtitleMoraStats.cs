using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtitleMoraStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SpeechDuration",
                schema: "jiten",
                table: "Decks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SpeechMoraCount",
                schema: "jiten",
                table: "Decks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpeechDuration",
                schema: "jiten",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "SpeechMoraCount",
                schema: "jiten",
                table: "Decks");
        }
    }
}
