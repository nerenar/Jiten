using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class UserIdUuid : Migration
    {
        /// <inheritdoc />
               protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop all foreign key constraints
            migrationBuilder.Sql(@"
                ALTER TABLE ""user"".""AspNetUserClaims"" DROP CONSTRAINT IF EXISTS ""FK_AspNetUserClaims_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""AspNetUserLogins"" DROP CONSTRAINT IF EXISTS ""FK_AspNetUserLogins_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""AspNetUserRoles"" DROP CONSTRAINT IF EXISTS ""FK_AspNetUserRoles_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""AspNetUserTokens"" DROP CONSTRAINT IF EXISTS ""FK_AspNetUserTokens_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""ApiKeys"" DROP CONSTRAINT IF EXISTS ""FK_ApiKeys_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""RefreshTokens"" DROP CONSTRAINT IF EXISTS ""FK_RefreshTokens_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""FsrsCards"" DROP CONSTRAINT IF EXISTS ""FK_FsrsCards_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""UserCoverages"" DROP CONSTRAINT IF EXISTS ""FK_UserCoverages_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""UserKnownWords"" DROP CONSTRAINT IF EXISTS ""FK_UserKnownWords_AspNetUsers_UserId"";
                ALTER TABLE ""user"".""UserMetadatas"" DROP CONSTRAINT IF EXISTS ""FK_UserMetadatas_AspNetUsers_UserId"";
            ");

            // Step 2: Add temporary GUID columns
            migrationBuilder.AddColumn<Guid>(
                name: "IdTemp",
                schema: "user",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "AspNetUserClaims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "AspNetUserLogins",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "AspNetUserRoles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "AspNetUserTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "ApiKeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "FsrsCards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "UserCoverages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "UserKnownWords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdTemp",
                schema: "user",
                table: "UserMetadatas",
                type: "uuid",
                nullable: true);

            // Step 3: Convert data to temporary columns
            migrationBuilder.Sql(@"
                UPDATE ""user"".""AspNetUsers"" SET ""IdTemp"" = ""Id""::uuid;
                
                UPDATE ""user"".""AspNetUserClaims"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""AspNetUserLogins"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""AspNetUserRoles"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""AspNetUserTokens"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""ApiKeys"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""RefreshTokens"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""FsrsCards"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""UserCoverages"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""UserKnownWords"" SET ""UserIdTemp"" = ""UserId""::uuid;
                UPDATE ""user"".""UserMetadatas"" SET ""UserIdTemp"" = ""UserId""::uuid;
            ");

            // Step 4: Drop old string columns
            migrationBuilder.DropColumn(
                name: "Id",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "AspNetUserClaims");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "AspNetUserLogins");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "AspNetUserRoles");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "AspNetUserTokens");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "FsrsCards");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "UserCoverages");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "UserKnownWords");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "user",
                table: "UserMetadatas");

            // Step 5: Rename temporary columns to original names
            migrationBuilder.RenameColumn(
                name: "IdTemp",
                schema: "user",
                table: "AspNetUsers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "AspNetUserClaims",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "AspNetUserLogins",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "AspNetUserRoles",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "AspNetUserTokens",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "ApiKeys",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "RefreshTokens",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "FsrsCards",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "UserCoverages",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "UserKnownWords",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UserIdTemp",
                schema: "user",
                table: "UserMetadatas",
                newName: "UserId");

            // Step 6: Make columns non-nullable
            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "user",
                table: "AspNetUsers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "AspNetUserClaims",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "AspNetUserLogins",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "AspNetUserRoles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "AspNetUserTokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "ApiKeys",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "FsrsCards",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "UserCoverages",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "UserKnownWords",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "user",
                table: "UserMetadatas",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Step 7: Recreate foreign key constraints
            migrationBuilder.Sql(@"
                ALTER TABLE ""user"".""AspNetUsers"" ADD PRIMARY KEY (""Id"");

                ALTER TABLE ""user"".""AspNetUserClaims"" 
                    ADD CONSTRAINT ""FK_AspNetUserClaims_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""AspNetUserLogins"" 
                    ADD CONSTRAINT ""FK_AspNetUserLogins_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""AspNetUserRoles"" 
                    ADD CONSTRAINT ""FK_AspNetUserRoles_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""AspNetUserTokens"" 
                    ADD CONSTRAINT ""FK_AspNetUserTokens_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""ApiKeys"" 
                    ADD CONSTRAINT ""FK_ApiKeys_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""RefreshTokens"" 
                    ADD CONSTRAINT ""FK_RefreshTokens_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""FsrsCards"" 
                    ADD CONSTRAINT ""FK_FsrsCards_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""UserCoverages"" 
                    ADD CONSTRAINT ""FK_UserCoverages_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""UserKnownWords"" 
                    ADD CONSTRAINT ""FK_UserKnownWords_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""user"".""UserMetadatas"" 
                    ADD CONSTRAINT ""FK_UserMetadatas_AspNetUsers_UserId"" 
                    FOREIGN KEY (""UserId"") REFERENCES ""user"".""AspNetUsers"" (""Id"") ON DELETE CASCADE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "UserMetadatas",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "UserKnownWords",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "UserCoverages",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "RefreshTokens",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "FsrsCards",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "AspNetUserTokens",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "Id",
                                                 schema: "user",
                                                 table: "AspNetUsers",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "AspNetUserRoles",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "AspNetUserLogins",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "AspNetUserClaims",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                                                 name: "UserId",
                                                 schema: "user",
                                                 table: "ApiKeys",
                                                 type: "text",
                                                 nullable: false,
                                                 oldClrType: typeof(Guid),
                                                 oldType: "uuid");
        }
    }
}