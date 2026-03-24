using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class AddMissingUserForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_FsrsCards_AspNetUsers_UserId') THEN
                        ALTER TABLE "user"."FsrsCards" ADD CONSTRAINT "FK_FsrsCards_AspNetUsers_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "user"."AspNetUsers" ("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_UserCoverageChunks_AspNetUsers_UserId') THEN
                        ALTER TABLE "user"."UserCoverageChunks" ADD CONSTRAINT "FK_UserCoverageChunks_AspNetUsers_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "user"."AspNetUsers" ("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_UserCoverages_AspNetUsers_UserId') THEN
                        ALTER TABLE "user"."UserCoverages" ADD CONSTRAINT "FK_UserCoverages_AspNetUsers_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "user"."AspNetUsers" ("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_UserKnownWords_AspNetUsers_UserId') THEN
                        ALTER TABLE "user"."UserKnownWords" ADD CONSTRAINT "FK_UserKnownWords_AspNetUsers_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "user"."AspNetUsers" ("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_UserWordSetStates_AspNetUsers_UserId') THEN
                        ALTER TABLE "user"."UserWordSetStates" ADD CONSTRAINT "FK_UserWordSetStates_AspNetUsers_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "user"."AspNetUsers" ("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FsrsCards_AspNetUsers_UserId",
                schema: "user",
                table: "FsrsCards");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCoverageChunks_AspNetUsers_UserId",
                schema: "user",
                table: "UserCoverageChunks");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCoverages_AspNetUsers_UserId",
                schema: "user",
                table: "UserCoverages");

            migrationBuilder.DropForeignKey(
                name: "FK_UserKnownWords_AspNetUsers_UserId",
                schema: "user",
                table: "UserKnownWords");

            migrationBuilder.DropForeignKey(
                name: "FK_UserWordSetStates_AspNetUsers_UserId",
                schema: "user",
                table: "UserWordSetStates");
        }
    }
}
