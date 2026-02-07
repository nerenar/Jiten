using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class DropParallelArrayColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObsoleteReadings",
                schema: "jmdict",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "ReadingTypes",
                schema: "jmdict",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "Readings",
                schema: "jmdict",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "ReadingsFurigana",
                schema: "jmdict",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "ReadingsFrequencyPercentage",
                schema: "jmdict",
                table: "WordFrequencies");

            migrationBuilder.DropColumn(
                name: "ReadingsFrequencyRank",
                schema: "jmdict",
                table: "WordFrequencies");

            migrationBuilder.DropColumn(
                name: "ReadingsObservedFrequency",
                schema: "jmdict",
                table: "WordFrequencies");

            migrationBuilder.DropColumn(
                name: "ReadingsUsedInMediaAmount",
                schema: "jmdict",
                table: "WordFrequencies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ObsoleteReadings",
                schema: "jmdict",
                table: "Words",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "ReadingTypes",
                schema: "jmdict",
                table: "Words",
                type: "int[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<List<string>>(
                name: "Readings",
                schema: "jmdict",
                table: "Words",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "ReadingsFurigana",
                schema: "jmdict",
                table: "Words",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<List<double>>(
                name: "ReadingsFrequencyPercentage",
                schema: "jmdict",
                table: "WordFrequencies",
                type: "double precision[]",
                nullable: false);

            migrationBuilder.AddColumn<List<int>>(
                name: "ReadingsFrequencyRank",
                schema: "jmdict",
                table: "WordFrequencies",
                type: "integer[]",
                nullable: false);

            migrationBuilder.AddColumn<List<double>>(
                name: "ReadingsObservedFrequency",
                schema: "jmdict",
                table: "WordFrequencies",
                type: "double precision[]",
                nullable: false);

            migrationBuilder.AddColumn<List<int>>(
                name: "ReadingsUsedInMediaAmount",
                schema: "jmdict",
                table: "WordFrequencies",
                type: "integer[]",
                nullable: false);
        }
    }
}
