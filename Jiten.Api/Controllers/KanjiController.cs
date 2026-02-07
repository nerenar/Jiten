using Jiten.Api.Dtos;
using Jiten.Api.Helpers;
using Jiten.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Jiten.Api.Controllers;

/// <summary>
/// Endpoints for working with kanji characters.
/// </summary>
[ApiController]
[Route("api/kanji")]
[EnableRateLimiting("fixed")]
[Produces("application/json")]
public class KanjiController(JitenDbContext context) : ControllerBase
{
    /// <summary>
    /// Gets a kanji by its character, including readings, meanings, metadata and top words.
    /// </summary>
    /// <param name="character">The kanji character.</param>
    /// <returns>The full kanji data with top words.</returns>
    [HttpGet("{character}")]
    [SwaggerOperation(Summary = "Get kanji by character",
                      Description =
                          "Returns a kanji with readings, meanings, stroke count, JLPT level, grade, frequency rank, and top 20 words containing it.")]
    [ProducesResponseType(typeof(KanjiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 3600)]
    public async Task<IResult> GetKanji([FromRoute] string character)
    {
        var kanji = await context.Kanjis
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(k => k.Character == character);

        if (kanji == null)
            return Results.NotFound();

        // Top 20 words selected in SQL via join + PostgreSQL array indexing
        var topWordData = await context.WordKanjis
                                        .AsNoTracking()
                                        .Where(wk => wk.KanjiCharacter == character)
                                        .Select(wk => new { wk.WordId, wk.ReadingIndex })
                                        .Distinct()
                                        .Join(context.WordFormFrequencies.AsNoTracking(),
                                              wk => new { wk.WordId, ReadingIndex = (short)wk.ReadingIndex },
                                              wff => new { wff.WordId, wff.ReadingIndex },
                                              (wk, wff) => new { wk.WordId, wk.ReadingIndex, Rank = (int?)wff.FrequencyRank })
                                        .Where(x => x.Rank > 0)
                                        .OrderBy(x => x.Rank)
                                        .Take(20)
                                        .ToListAsync();

        var topWordIds = topWordData.Select(x => x.WordId).Distinct().ToList();
        var words = await context.JMDictWords
                                 .AsNoTracking()
                                 .Include(w => w.Definitions)
                                 .Where(w => topWordIds.Contains(w.WordId))
                                 .ToDictionaryAsync(w => w.WordId);

        var forms = await WordFormHelper.LoadWordForms(context, topWordIds);

        var topWords = topWordData
                       .Where(x => words.ContainsKey(x.WordId))
                       .Select(x =>
                       {
                           var word = words[x.WordId];
                           var form = forms.GetValueOrDefault((x.WordId, (short)x.ReadingIndex));
                           var mainDefinition = word.Definitions.FirstOrDefault()?.EnglishMeanings.FirstOrDefault();
                           return new WordSummaryDto
                                  {
                                      WordId = x.WordId, ReadingIndex = (byte)x.ReadingIndex, Reading = form?.Text ?? "",
                                      ReadingFurigana = form?.RubyText ?? "", MainDefinition = mainDefinition,
                                      FrequencyRank = x.Rank!.Value
                                  };
                       })
                       .ToList();

        return Results.Ok(new KanjiDto
                          {
                              Character = kanji.Character, OnReadings = kanji.OnReadings, KunReadings = kanji.KunReadings,
                              Meanings = kanji.Meanings, StrokeCount = kanji.StrokeCount, JlptLevel = kanji.JlptLevel, Grade = kanji.Grade,
                              FrequencyRank = kanji.FrequencyRank, TopWords = topWords
                          });
    }

    /// <summary>
    /// Gets a paginated list of words containing a specific kanji.
    /// </summary>
    /// <param name="character">The kanji character.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <returns>Paginated list of words.</returns>
    [HttpGet("{character}/words")]
    [SwaggerOperation(Summary = "Get words containing kanji",
                      Description =
                          "Returns a paginated list of words containing the specified kanji, ordered by reading-specific frequency.")]
    [ProducesResponseType(typeof(PaginatedResponse<List<WordSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 3600, VaryByQueryKeys = ["page"])]
    public async Task<IResult> GetKanjiWords(
        [FromRoute] string character,
        [FromQuery] int page = 1)
    {
        var kanjiExists = await context.Kanjis.AnyAsync(k => k.Character == character);
        if (!kanjiExists)
            return Results.NotFound();

        var pageSize = 100;
        page = Math.Max(page, 1);

        // Rank words in SQL via join + PostgreSQL array indexing
        var rankedQuery = context.WordKanjis
                                  .AsNoTracking()
                                  .Where(wk => wk.KanjiCharacter == character)
                                  .Select(wk => new { wk.WordId, wk.ReadingIndex })
                                  .Distinct()
                                  .Join(context.WordFormFrequencies.AsNoTracking(),
                                        wk => new { wk.WordId, ReadingIndex = (short)wk.ReadingIndex },
                                        wff => new { wff.WordId, wff.ReadingIndex },
                                        (wk, wff) => new { wk.WordId, wk.ReadingIndex, Rank = (int?)wff.FrequencyRank })
                                  .Where(x => x.Rank > 0);

        var totalCount = await rankedQuery.CountAsync();

        var pageData = await rankedQuery
                              .OrderBy(x => x.Rank)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync();

        var pageWordIds = pageData.Select(x => x.WordId).Distinct().ToList();
        var words = await context.JMDictWords
                                 .AsNoTracking()
                                 .Include(w => w.Definitions)
                                 .Where(w => pageWordIds.Contains(w.WordId))
                                 .ToDictionaryAsync(w => w.WordId);

        var forms = await WordFormHelper.LoadWordForms(context, pageWordIds);

        var items = pageData
                    .Where(x => words.ContainsKey(x.WordId))
                    .Select(x =>
                    {
                        var word = words[x.WordId];
                        var form = forms.GetValueOrDefault((x.WordId, (short)x.ReadingIndex));
                        var mainDefinition = word.Definitions.FirstOrDefault()?.EnglishMeanings.FirstOrDefault();
                        return new WordSummaryDto
                               {
                                   WordId = x.WordId, ReadingIndex = (byte)x.ReadingIndex, Reading = form?.Text ?? "",
                                   ReadingFurigana = form?.RubyText ?? "", MainDefinition = mainDefinition,
                                   FrequencyRank = x.Rank!.Value
                               };
                    })
                    .ToList();

        return Results.Ok(new PaginatedResponse<List<WordSummaryDto>>(
                                                                      items,
                                                                      totalCount,
                                                                      pageSize,
                                                                      (page - 1) * pageSize
                                                                     ));
    }
}