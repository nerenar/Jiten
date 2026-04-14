using Jiten.Api.Dtos;
using Jiten.Core;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Helpers;

public static class CompositionHelper
{
    public static async Task<List<WordSummaryDto>?> LoadComposedOf(
        IDbContextFactory<JitenDbContext> contextFactory, int wordId, short readingIndex)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();

        var rows = await ctx.WordCompositions
            .AsNoTracking()
            .Where(c => c.WordId == wordId && c.ReadingIndex == readingIndex)
            .OrderBy(c => c.Position)
            .Select(c => new
            {
                c.Position,
                c.ComponentWordId,
                c.ComponentReadingIndex,
                c.ComponentSurface
            })
            .ToListAsync();

        if (rows.Count == 0) return null;

        var componentIds = rows.Select(r => r.ComponentWordId).Distinct().ToList();

        var definitions = await ctx.Definitions
            .AsNoTracking()
            .Where(d => componentIds.Contains(d.WordId))
            .OrderBy(d => d.WordId).ThenBy(d => d.SenseIndex)
            .Select(d => new { d.WordId, d.EnglishMeanings })
            .ToListAsync();

        var firstDefByWord = definitions
            .GroupBy(d => d.WordId)
            .ToDictionary(g => g.Key, g => g.First().EnglishMeanings.FirstOrDefault());

        var forms = await WordFormHelper.LoadWordForms(ctx, componentIds);

        var freqs = await ctx.WordFormFrequencies
            .AsNoTracking()
            .Where(wff => componentIds.Contains(wff.WordId))
            .ToDictionaryAsync(wff => (wff.WordId, wff.ReadingIndex));

        return rows.Select(r =>
        {
            var form = forms.GetValueOrDefault((r.ComponentWordId, r.ComponentReadingIndex));
            var freq = freqs.GetValueOrDefault((r.ComponentWordId, r.ComponentReadingIndex));
            return new WordSummaryDto
            {
                WordId = r.ComponentWordId,
                ReadingIndex = (byte)r.ComponentReadingIndex,
                Reading = form?.Text ?? r.ComponentSurface,
                ReadingFurigana = form?.RubyText ?? r.ComponentSurface,
                MainDefinition = firstDefByWord.GetValueOrDefault(r.ComponentWordId),
                FrequencyRank = freq?.FrequencyRank > 0 ? freq.FrequencyRank : null
            };
        }).ToList();
    }
}
