using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class FsrsPerformanceOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BRIN indexes for time-based queries
            migrationBuilder.Sql(@"
                CREATE INDEX ix_fsrscards_due_brin ON ""user"".""FsrsCards"" 
                USING brin (""Due"") 
                WITH (
                    pages_per_range = 128,
                    autosummarize = on
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX ix_fsrsreviewlogs_datetime_brin ON ""user"".""FsrsReviewLogs"" 
                USING brin (""ReviewDateTime"") 
                WITH (
                    pages_per_range = 128,
                    autosummarize = on
                );
            ");

            // Fill factor for update performance
            migrationBuilder.Sql(@"
                ALTER TABLE ""user"".""FsrsCards"" SET (fillfactor = 90);
            ");

            // Autovacuum settings
            migrationBuilder.Sql(@"
                ALTER TABLE ""user"".""FsrsCards"" SET (
                    autovacuum_vacuum_scale_factor = 0.05,
                    autovacuum_analyze_scale_factor = 0.02
                );
                
                ALTER TABLE ""user"".""FsrsReviewLogs"" SET (
                    autovacuum_vacuum_scale_factor = 0.1,
                    autovacuum_analyze_scale_factor = 0.05
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all the indexes and revert settings
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""user"".ix_fsrscards_due_brin;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""user"".ix_fsrsreviewlogs_datetime_brin;");

            // Revert table settings to defaults
            migrationBuilder
                .Sql(@"ALTER TABLE ""user"".""FsrsCards"" RESET (toast_compression, fillfactor, autovacuum_vacuum_scale_factor, autovacuum_analyze_scale_factor);");
            migrationBuilder
                .Sql(@"ALTER TABLE ""user"".""FsrsReviewLogs"" RESET (toast_compression, autovacuum_vacuum_scale_factor, autovacuum_analyze_scale_factor);");
        }
    }
}
