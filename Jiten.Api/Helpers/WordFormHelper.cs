using Jiten.Api.Dtos;
using Jiten.Core;
using Jiten.Core.Data.JMDict;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Helpers;

public static class WordFormHelper
{
    public static WordFormDto ToFormDto(JmDictWordForm form, JmDictWordFormFrequency? freq, Dictionary<int, int>? usedInMediaByType = null)
    {
        return new WordFormDto
        {
            Text = form.RubyText,
            ReadingIndex = (byte)form.ReadingIndex,
            ReadingType = (JmDictReadingType)(int)form.FormType,
            FrequencyRank = freq?.FrequencyRank ?? 0,
            FrequencyPercentage = freq?.FrequencyPercentage ?? 0,
            UsedInMediaAmount = freq?.UsedInMediaAmount ?? 0,
            UsedInMediaAmountByType = usedInMediaByType ?? new()
        };
    }

    public static WordFormDto ToPlainFormDto(JmDictWordForm form, JmDictWordFormFrequency? freq)
    {
        return new WordFormDto
        {
            Text = form.Text,
            ReadingIndex = (byte)form.ReadingIndex,
            ReadingType = (JmDictReadingType)(int)form.FormType,
            FrequencyRank = freq?.FrequencyRank ?? 0,
            FrequencyPercentage = freq?.FrequencyPercentage ?? 0,
            UsedInMediaAmount = freq?.UsedInMediaAmount ?? 0
        };
    }

    public static async Task<Dictionary<(int, short), JmDictWordForm>> LoadWordForms(JitenDbContext context, List<int> wordIds)
    {
        return await context.WordForms
            .AsNoTracking()
            .Where(wf => wordIds.Contains(wf.WordId))
            .ToDictionaryAsync(wf => (wf.WordId, wf.ReadingIndex));
    }

    public static async Task<Dictionary<(int, short), JmDictWordFormFrequency>> LoadWordFormFrequencies(JitenDbContext context, List<int> wordIds)
    {
        return await context.WordFormFrequencies
            .AsNoTracking()
            .Where(wff => wordIds.Contains(wff.WordId))
            .ToDictionaryAsync(wff => (wff.WordId, wff.ReadingIndex));
    }

    public static long EncodeWordKey(int wordId, byte readingIndex)
        => ((long)wordId << 8) | readingIndex;

    public static long EncodeWordKey(int wordId, short readingIndex)
        => ((long)wordId << 8) | (byte)readingIndex;

    public static async Task<HashSet<int>> GetKanaOnlyWordIds(JitenDbContext context, IEnumerable<int> wordIds)
    {
        var distinctIds = wordIds.Distinct().ToList();
        if (distinctIds.Count == 0) return [];

        var formTypes = await context.WordForms.AsNoTracking()
            .Where(wf => distinctIds.Contains(wf.WordId))
            .Select(wf => new { wf.WordId, wf.FormType })
            .ToListAsync();
        return formTypes
            .GroupBy(wf => wf.WordId)
            .Where(g => g.All(wf => wf.FormType == JmDictFormType.KanaForm))
            .Select(g => g.Key)
            .ToHashSet();
    }

    public static async Task<List<JmDictWordForm>> LoadWordFormsForWord(JitenDbContext context, int wordId)
    {
        return await context.WordForms
            .AsNoTracking()
            .Where(wf => wf.WordId == wordId)
            .OrderBy(wf => wf.ReadingIndex)
            .ToListAsync();
    }
}
