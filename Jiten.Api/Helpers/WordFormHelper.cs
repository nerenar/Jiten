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

    public static async Task<List<JmDictWordForm>> LoadWordFormsForWord(JitenDbContext context, int wordId)
    {
        return await context.WordForms
            .AsNoTracking()
            .Where(wf => wf.WordId == wordId)
            .OrderBy(wf => wf.ReadingIndex)
            .ToListAsync();
    }
}
