using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class AddUserLastActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivity",
                schema: "user",
                table: "UserMetadatas",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill: ensure a UserMetadata row exists for every user with any activity data,
            // then populate LastActivity from the most recent FSRS review/creation or WordSet subscription.
            migrationBuilder.Sql("""
                INSERT INTO "user"."UserMetadatas" ("UserId", "CoverageDirty")
                SELECT DISTINCT s."UserId", false
                FROM (
                    SELECT "UserId" FROM "user"."FsrsCards"
                    UNION
                    SELECT "UserId" FROM "user"."UserWordSetStates"
                ) s
                WHERE NOT EXISTS (
                    SELECT 1 FROM "user"."UserMetadatas" um WHERE um."UserId" = s."UserId"
                );
                """);

            migrationBuilder.Sql("""
                UPDATE "user"."UserMetadatas" um
                SET "LastActivity" = sub.last_activity
                FROM (
                    SELECT "UserId", MAX(ts) AS last_activity
                    FROM (
                        SELECT "UserId", GREATEST(COALESCE("LastReview", "CreatedAt"), "CreatedAt") AS ts
                        FROM "user"."FsrsCards"
                        UNION ALL
                        SELECT "UserId", "CreatedAt" AS ts FROM "user"."UserWordSetStates"
                    ) s
                    GROUP BY "UserId"
                ) sub
                WHERE um."UserId" = sub."UserId"
                  AND (um."LastActivity" IS NULL OR um."LastActivity" < sub.last_activity);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastActivity",
                schema: "user",
                table: "UserMetadatas");
        }
    }
}
