using Jiten.Api.Dtos;
using Jiten.Api.Services;
using Jiten.Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jiten.Api.Controllers;

public partial class AdminController
{
    private const string PGroongaExact = "&@";
    private const string PGroongaRegex = "&~";

    private static string MatchOp(bool useRegex) => useRegex ? PGroongaRegex : PGroongaExact;

    private static async Task DisableSeqScan(NpgsqlConnection conn)
    {
        await using var setCmd = conn.CreateCommand();
        setCmd.CommandText = "SET LOCAL enable_seqscan = off";
        await setCmd.ExecuteNonQueryAsync();
    }

    [HttpPost("corpus/search")]
    public async Task<IActionResult> CorpusSearch([FromBody] CorpusSearchRequest request)
    {
        var validationError = ValidateCorpusRequest(request);
        if (validationError != null) return validationError;

        return Ok(await BuildSearchResponse(request));
    }

    [HttpPost("corpus/export")]
    public async Task<IActionResult> CorpusExport([FromBody] CorpusSearchRequest request)
    {
        var validationError = ValidateCorpusRequest(request);
        if (validationError != null) return validationError;

        var searchResponse = await BuildSearchResponse(request);

        var coOccurrences = new List<CorpusCoOccurrence>();
        if (request.Terms.Count > 1)
            coOccurrences = await GetCoOccurrences(request.Terms, request);

        var html = CorpusReportService.GenerateReport(searchResponse, coOccurrences, request);
        return File(System.Text.Encoding.UTF8.GetBytes(html), "text/html", $"corpus-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.html");
    }

    [HttpPost("corpus/co-occurrences")]
    public async Task<IActionResult> CorpusCoOccurrences([FromBody] CorpusSearchRequest request)
    {
        if (request.Terms is not { Count: >= 2 and <= 5 })
            return BadRequest("Provide 2-5 terms for co-occurrence analysis.");

        var coOccurrences = await GetCoOccurrences(request.Terms, request);
        return Ok(coOccurrences);
    }

    private async Task<CorpusSearchResponse> BuildSearchResponse(CorpusSearchRequest request)
    {
        var statsTask = GetCorpusStats();
        var resultsTask = SearchTermsParallel(request);
        await Task.WhenAll(statsTask, resultsTask);
        return new CorpusSearchResponse
        {
            Results = resultsTask.Result,
            CorpusStats = statsTask.Result
        };
    }

    private async Task<List<CorpusTermResult>> SearchTermsParallel(CorpusSearchRequest request)
    {
        var connString = config.GetConnectionString("JitenDatabase")!;

        Dictionary<int, long> yearTotals;
        await using (var conn = new NpgsqlConnection(connString))
        {
            await conn.OpenAsync();
            yearTotals = await GetYearTotals(conn);
        }

        var tasks = request.Terms.Select(async term =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            await DisableSeqScan(conn);
            return await SearchTerm(conn, term, request, yearTotals);
        });
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<Dictionary<int, long>> GetYearTotals(System.Data.Common.DbConnection conn)
    {
        var dict = new Dictionary<int, long>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXTRACT(YEAR FROM d."ReleaseDate")::int AS yr, SUM(d."CharacterCount")::bigint AS total_chars
            FROM jiten."Decks" d
            JOIN jiten."DeckRawTexts" drt ON drt."DeckId" = d."DeckId"
            WHERE d."ReleaseDate" > '0001-01-01' AND isfinite(d."ReleaseDate")
            GROUP BY EXTRACT(YEAR FROM d."ReleaseDate")
            """;
        cmd.CommandTimeout = 30;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            dict[reader.GetInt32(0)] = reader.GetInt64(1);
        return dict;
    }

    private async Task<CorpusTermResult> SearchTerm(System.Data.Common.DbConnection conn, string term, CorpusSearchRequest request, Dictionary<int, long> yearTotals)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var matchOperator = MatchOp(request.UseRegex);
        var filterClauses = BuildFilterClauses(request);

        var snippets = await GetSnippets(conn, term, matchOperator, filterClauses, request);
        logger.LogDebug("[Corpus] '{Term}' snippets: {Ms}ms", term, sw.ElapsedMilliseconds);

        var stats = await GetCombinedStats(conn, term, matchOperator, filterClauses);
        logger.LogDebug("[Corpus] '{Term}' stats: {Ms}ms", term, sw.ElapsedMilliseconds);

        var ngramTrends = await GetNgramTrends(conn, term, matchOperator, filterClauses, yearTotals);
        logger.LogDebug("[Corpus] '{Term}' ngram: {Ms}ms ({Count} points)", term, sw.ElapsedMilliseconds, ngramTrends.Count);

        var topDecks = await GetTopDecks(conn, term, matchOperator, filterClauses);
        logger.LogDebug("[Corpus] '{Term}' topDecks: {Ms}ms", term, sw.ElapsedMilliseconds);

        var totalDecks = stats.MediaBreakdown.Sum(m => m.DeckCount);
        var totalChars = stats.MediaBreakdown.Sum(m => m.TotalCharacters);

        return new CorpusTermResult
        {
            Term = term,
            MatchingDecks = totalDecks,
            HitsPerMillion = totalChars > 0 ? totalDecks / (totalChars / 1_000_000.0) : 0,
            Snippets = snippets,
            MediaBreakdown = stats.MediaBreakdown,
            Trends = ngramTrends,
            DifficultyDistribution = stats.DifficultyDistribution,
            TopDecks = topDecks,
            DialogueWeightedAvg = stats.DialogueAvg
        };
    }

    private async Task<List<CorpusSnippet>> GetSnippets(
        System.Data.Common.DbConnection conn, string term, string matchOp,
        (string sql, List<NpgsqlParameter> parameters) filters, CorpusSearchRequest request)
    {
        var sql = $"""
            WITH matched AS MATERIALIZED (
                SELECT drt."DeckId", drt."RawText"
                FROM jiten."DeckRawTexts" drt
                WHERE drt."RawText" {matchOp} @term
            )
            SELECT
                pgroonga_snippet_html(m."RawText", ARRAY[@term], 160) AS snippets,
                d."DeckId",
                COALESCE(d."OriginalTitle", 'Unknown') AS "DeckTitle",
                d."MediaType",
                d."Difficulty",
                CASE WHEN d."ReleaseDate" IS NOT NULL AND isfinite(d."ReleaseDate") THEN EXTRACT(YEAR FROM d."ReleaseDate")::int ELSE 0 END AS "ReleaseYear"
            FROM matched m
            JOIN jiten."Decks" d ON d."DeckId" = m."DeckId"
            WHERE 1=1
            {filters.sql}
            LIMIT @limit
            """;

        var snippets = new List<CorpusSnippet>();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;

        cmd.Parameters.Add(new NpgsqlParameter("@term", term));
        cmd.Parameters.Add(new NpgsqlParameter("@limit", request.MaxSnippets));
        foreach (var p in filters.parameters)
            cmd.Parameters.Add(CloneParameter(p));

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var snippetArray = reader.GetFieldValue<string[]>(0);
            var deckId = reader.GetInt32(1);
            var deckTitle = reader.GetString(2);
            var mediaType = (MediaType)reader.GetInt32(3);
            var difficulty = reader.GetFloat(4);
            var releaseYear = reader.GetInt32(5);

            snippets.Add(new CorpusSnippet
            {
                Html = snippetArray[0],
                DeckId = deckId,
                DeckTitle = deckTitle,
                MediaType = mediaType,
                Difficulty = difficulty,
                ReleaseYear = releaseYear
            });
        }

        return snippets;
    }

    private async Task<List<CorpusTopDeck>> GetTopDecks(
        System.Data.Common.DbConnection conn, string term, string matchOp,
        (string sql, List<NpgsqlParameter> parameters) filters)
    {
        var sql = $"""
            WITH matched AS MATERIALIZED (
                SELECT drt."DeckId", pgroonga_score(drt.tableoid, drt.ctid) AS score
                FROM jiten."DeckRawTexts" drt
                WHERE drt."RawText" {matchOp} @term
            )
            SELECT
                d."DeckId",
                COALESCE(d."OriginalTitle", 'Unknown') AS title,
                p."OriginalTitle" AS parent_title,
                d."MediaType",
                m.score::int AS occurrences,
                d."CharacterCount"
            FROM matched m
            JOIN jiten."Decks" d ON d."DeckId" = m."DeckId"
            LEFT JOIN jiten."Decks" p ON p."DeckId" = d."ParentDeckId"
            WHERE 1=1
            {filters.sql}
            ORDER BY m.score DESC
            LIMIT 20
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        cmd.Parameters.Add(new NpgsqlParameter("@term", term));
        foreach (var p in filters.parameters)
            cmd.Parameters.Add(CloneParameter(p));

        var results = new List<CorpusTopDeck>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var charCount = reader.GetInt32(5);
            var occ = reader.GetInt32(4);
            results.Add(new CorpusTopDeck
            {
                DeckId = reader.GetInt32(0),
                Title = reader.GetString(1),
                ParentTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                MediaType = (MediaType)reader.GetInt32(3),
                Occurrences = occ,
                PerMillion = charCount > 0 ? occ / (charCount / 1_000_000.0) : 0
            });
        }

        return results;
    }

    private async Task<List<CorpusTrendPoint>> GetNgramTrends(
        System.Data.Common.DbConnection conn, string term, string matchOp,
        (string sql, List<NpgsqlParameter> parameters) filters, Dictionary<int, long> yearTotals)
    {
        var sql = $"""
            WITH matched AS MATERIALIZED (
                SELECT drt."DeckId", pgroonga_score(drt.tableoid, drt.ctid) AS score
                FROM jiten."DeckRawTexts" drt
                WHERE drt."RawText" {matchOp} @term
            )
            SELECT
                EXTRACT(YEAR FROM d."ReleaseDate")::int AS yr,
                SUM(m.score)::bigint AS total_occ
            FROM matched m
            JOIN jiten."Decks" d ON d."DeckId" = m."DeckId"
            WHERE d."ReleaseDate" > '0001-01-01'
              AND isfinite(d."ReleaseDate")
            {filters.sql}
            GROUP BY EXTRACT(YEAR FROM d."ReleaseDate")
            ORDER BY yr
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        cmd.Parameters.Add(new NpgsqlParameter("@term", term));
        foreach (var p in filters.parameters)
            cmd.Parameters.Add(CloneParameter(p));

        const long minCharsPerYear = 500_000;

        var trends = new List<CorpusTrendPoint>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var year = reader.GetInt32(0);
            var occurrences = reader.GetInt64(1);
            if (!yearTotals.TryGetValue(year, out var totalCharsInYear) || totalCharsInYear < minCharsPerYear)
                continue;
            trends.Add(new CorpusTrendPoint
            {
                Year = year,
                MatchingChars = occurrences,
                TotalCharsInYear = totalCharsInYear,
                Percentage = occurrences / (totalCharsInYear / 1_000_000.0)
            });
        }

        return trends;
    }

    private record CombinedStatsResult(
        List<CorpusMediaBreakdown> MediaBreakdown,
        List<CorpusDifficultyBucket> DifficultyDistribution,
        double DialogueAvg);

    private async Task<CombinedStatsResult> GetCombinedStats(
        System.Data.Common.DbConnection conn, string term, string matchOp,
        (string sql, List<NpgsqlParameter> parameters) filters)
    {
        var sql = $"""
            WITH matched AS MATERIALIZED (
                SELECT drt."DeckId"
                FROM jiten."DeckRawTexts" drt
                WHERE drt."RawText" {matchOp} @term
            ),
            deck_data AS (
                SELECT d."DeckId", d."MediaType", d."CharacterCount", d."Difficulty",
                       d."DialoguePercentage"
                FROM matched m
                JOIN jiten."Decks" d ON d."DeckId" = m."DeckId"
                WHERE 1=1
                {filters.sql}
            ),
            media_stats AS (
                SELECT 1 AS result_set, "MediaType"::int AS key1,
                       COUNT(*)::bigint AS val1, SUM("CharacterCount")::bigint AS val2, 0::float8 AS val3
                FROM deck_data GROUP BY "MediaType"
            ),
            diff_stats AS (
                SELECT 2 AS result_set, FLOOR("Difficulty")::int AS key1,
                       COUNT(*)::bigint AS val1, 0::bigint AS val2, 0::float8 AS val3
                FROM deck_data WHERE "Difficulty" > 0
                GROUP BY FLOOR("Difficulty")
            ),
            dialogue_stats AS (
                SELECT 3 AS result_set, 0 AS key1,
                       0::bigint AS val1, 0::bigint AS val2, COALESCE(AVG("DialoguePercentage"), 0)::float8 AS val3
                FROM deck_data
            )
            SELECT * FROM media_stats
            UNION ALL SELECT * FROM diff_stats
            UNION ALL SELECT * FROM dialogue_stats
            ORDER BY result_set, key1
            """;

        var mediaBreakdown = new List<CorpusMediaBreakdown>();
        var diffDist = new List<CorpusDifficultyBucket>();
        double dialogueAvg = 0;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        cmd.Parameters.Add(new NpgsqlParameter("@term", term));
        foreach (var p in filters.parameters)
            cmd.Parameters.Add(CloneParameter(p));

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var resultSet = reader.GetInt32(0);
            switch (resultSet)
            {
                case 1:
                    mediaBreakdown.Add(new CorpusMediaBreakdown
                    {
                        MediaType = (MediaType)reader.GetInt32(1),
                        DeckCount = (int)reader.GetInt64(2),
                        TotalCharacters = reader.GetInt64(3)
                    });
                    break;
                case 2:
                    diffDist.Add(new CorpusDifficultyBucket
                    {
                        BucketMin = reader.GetInt32(1),
                        BucketMax = reader.GetInt32(1) + 1,
                        DeckCount = (int)reader.GetInt64(2)
                    });
                    break;
                case 3:
                    dialogueAvg = reader.GetDouble(4);
                    break;
            }
        }

        var totalDecks = mediaBreakdown.Sum(m => m.DeckCount);
        foreach (var m in mediaBreakdown)
        {
            m.HitsPerMillion = m.TotalCharacters > 0
                ? m.DeckCount / (m.TotalCharacters / 1_000_000.0) : 0;
            m.Percentage = totalDecks > 0 ? m.DeckCount * 100.0 / totalDecks : 0;
        }

        return new CombinedStatsResult(
            mediaBreakdown.OrderByDescending(m => m.DeckCount).ToList(),
            diffDist, dialogueAvg);
    }

    private async Task<List<CorpusCoOccurrence>> GetCoOccurrences(
        List<string> terms, CorpusSearchRequest request)
    {
        var connString = config.GetConnectionString("JitenDatabase")!;
        var filters = BuildFilterClauses(request);
        var matchOp = MatchOp(request.UseRegex);

        var pairs = new List<(int i, int j)>();
        for (int i = 0; i < terms.Count; i++)
            for (int j = i + 1; j < terms.Count; j++)
                pairs.Add((i, j));

        var tasks = pairs.Select(async pair =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            await DisableSeqScan(conn);

            var sql = $"""
                WITH matched AS MATERIALIZED (
                    SELECT drt."DeckId"
                    FROM jiten."DeckRawTexts" drt
                    WHERE drt."RawText" {matchOp} @termA
                      AND drt."RawText" {matchOp} @termB
                )
                SELECT COUNT(*)::int
                FROM matched m
                JOIN jiten."Decks" d ON d."DeckId" = m."DeckId"
                WHERE 1=1
                {filters.sql}
                """;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;
            cmd.Parameters.Add(new NpgsqlParameter("@termA", terms[pair.i]));
            cmd.Parameters.Add(new NpgsqlParameter("@termB", terms[pair.j]));
            foreach (var p in filters.parameters)
                cmd.Parameters.Add(CloneParameter(p));

            var count = (int)(await cmd.ExecuteScalarAsync())!;
            return new CorpusCoOccurrence
            {
                TermA = terms[pair.i],
                TermB = terms[pair.j],
                SharedDecks = count
            };
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private IActionResult? ValidateCorpusRequest(CorpusSearchRequest request)
    {
        if (request.Terms is not { Count: > 0 and <= 5 })
            return BadRequest("Provide 1-5 search terms.");
        if (request.Terms.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 200))
            return BadRequest("Each term must be 1-200 characters.");
        request.MaxSnippets = Math.Clamp(request.MaxSnippets, 1, 50);
        return null;
    }

    private async Task<CorpusStats> GetCorpusStats()
    {
        var deckStats = await dbContext.Decks
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), TotalChars = g.Sum(d => (long)d.CharacterCount) })
            .FirstOrDefaultAsync();
        var decksWithRawText = await dbContext.Set<DeckRawText>().CountAsync();

        return new CorpusStats
        {
            TotalDecks = deckStats?.Count ?? 0,
            TotalCharacters = deckStats?.TotalChars ?? 0,
            DecksWithRawText = decksWithRawText
        };
    }

    private (string sql, List<NpgsqlParameter> parameters) BuildFilterClauses(CorpusSearchRequest request)
    {
        var clauses = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        if (request.MediaTypes is { Count: > 0 })
        {
            clauses.Add("""AND d."MediaType" = ANY(@mediaTypes)""");
            parameters.Add(new NpgsqlParameter("@mediaTypes", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer)
                { Value = request.MediaTypes.Select(m => (int)m).ToArray() });
        }

        if (request.MinDifficulty.HasValue)
        {
            clauses.Add("""AND d."Difficulty" >= @minDiff""");
            parameters.Add(new NpgsqlParameter("@minDiff", request.MinDifficulty.Value));
        }

        if (request.MaxDifficulty.HasValue)
        {
            clauses.Add("""AND d."Difficulty" <= @maxDiff""");
            parameters.Add(new NpgsqlParameter("@maxDiff", request.MaxDifficulty.Value));
        }

        if (request.MinReleaseYear.HasValue)
        {
            clauses.Add("""AND EXTRACT(YEAR FROM d."ReleaseDate") >= @minYear""");
            parameters.Add(new NpgsqlParameter("@minYear", request.MinReleaseYear.Value));
        }

        if (request.MaxReleaseYear.HasValue)
        {
            clauses.Add("""AND EXTRACT(YEAR FROM d."ReleaseDate") <= @maxYear""");
            parameters.Add(new NpgsqlParameter("@maxYear", request.MaxReleaseYear.Value));
        }

        return (string.Join("\n", clauses), parameters);
    }

    private static NpgsqlParameter CloneParameter(NpgsqlParameter source) =>
        new(source.ParameterName, source.Value) { NpgsqlDbType = source.NpgsqlDbType };
}
