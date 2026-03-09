using Jiten.Core;
using Microsoft.EntityFrameworkCore;
using WanaKanaShaapu;

namespace Jiten.Api.Helpers;

public static class SearchHelper
{
    public static bool ContainsJapanese(string text)
    {
        return text.Any(c =>
                            c is >= '\u3040' and <= '\u309F'
                                or >= '\u30A0' and <= '\u30FF'
                                or >= '\u4E00' and <= '\u9FFF'
                                or >= '\u3400' and <= '\u4DBF');
    }

    public static string SanitizeLikeInput(string input)
    {
        return input
               .Replace("\\", "\\\\")
               .Replace("%", "\\%")
               .Replace("_", "\\_");
    }

    public static async Task<HashSet<int>> ResolveSearchWordIds(JitenDbContext context, string search)
    {
        var trimmed = search.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return [];

        var sanitized = SanitizeLikeInput(trimmed);
        var isAsciiOnly = trimmed.All(c => c < 128);
        var hasSpaces = trimmed.Contains(' ');

        string searchTerm;
        if (isAsciiOnly && !hasSpaces)
        {
            var hiragana = WanaKana.ToHiragana(trimmed.ToLowerInvariant());
            var isValidKana = hiragana.All(c =>
                                               c is >= '\u3040' and <= '\u309F' or >= '\u30A0' and <= '\u30FF');
            searchTerm = isValidKana ? hiragana : trimmed;
        }
        else
        {
            searchTerm = trimmed;
        }

        var sanitizedTerm = SanitizeLikeInput(searchTerm);
        var wordIds = await context.WordForms
                                   .AsNoTracking()
                                   .Where(wf => EF.Functions.Like(wf.Text, $"%{sanitizedTerm}%"))
                                   .Select(wf => wf.WordId)
                                   .Distinct()
                                   .ToListAsync();

        var result = new HashSet<int>(wordIds);

        if (isAsciiOnly)
        {
            var englishWordIds = await context.Definitions
                                              .AsNoTracking()
                                              .Where(d => d.EnglishMeanings.Any(m => EF.Functions.Like(m, $"%{sanitized}%")))
                                              .Select(d => d.WordId)
                                              .Distinct()
                                              .ToListAsync();

            result.UnionWith(englishWordIds);
        }

        return result;
    }
}