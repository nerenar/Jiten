using Jiten.Core;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Helpers;

public static class VocabularyFilterHelper
{
    public static string[] ParseCommaSeparatedTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static async Task<HashSet<int>?> GetPosFilteredWordIds(
        JitenDbContext context, string? posCsv, IEnumerable<int> candidateWordIds)
    {
        var tags = ParseCommaSeparatedTags(posCsv);
        if (tags.Length == 0) return null;

        var wordIdList = candidateWordIds.Distinct().ToList();
        return (await context.JMDictWords.AsNoTracking()
            .Where(w => wordIdList.Contains(w.WordId) && w.PartsOfSpeech.Any(p => tags.Contains(p)))
            .Select(w => w.WordId)
            .ToListAsync())
            .ToHashSet();
    }
}
