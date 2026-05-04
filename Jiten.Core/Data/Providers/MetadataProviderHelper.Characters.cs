using System.Text;
using Jiten.Core.Data;
using WanaKanaShaapu;

namespace Jiten.Core;

public static partial class MetadataProviderHelper
{
    public static List<DeckDictionaryEntry> BuildDictionaryEntriesFromNames(
        List<(string? native, string? firstHint, string? lastHint)> names)
    {
        var seen = new HashSet<string>();
        var entries = new List<DeckDictionaryEntry>();

        void Add(string surface)
        {
            var trimmed = surface.Trim();
            if (trimmed.Length >= 1 && seen.Add(trimmed))
                entries.Add(new DeckDictionaryEntry { Surface = trimmed, EntryType = DeckDictionaryEntryType.Name });
        }

        foreach (var (native, firstHint, lastHint) in names)
        {
            if (string.IsNullOrWhiteSpace(native)) continue;

            var fullName = native.Replace(" ", "").Replace("　", "").Trim();
            if (fullName.Length < 2) continue;

            Add(fullName);

            if (native.Contains('・'))
            {
                foreach (var part in native.Split('・', StringSplitOptions.RemoveEmptyEntries))
                {
                    var cleaned = part.Replace(" ", "").Replace("　", "").Trim();
                    if (cleaned.Length >= 2)
                        Add(cleaned);
                }
            }

            // Try splitting
            var parts = SplitName(native, firstHint, lastHint);
            if (parts != null)
            {
                Add(parts.Value.family);
                Add(parts.Value.given);
            }
        }

        return entries;
    }

    private static (string family, string given)? SplitName(string native, string? firstHint, string? lastHint)
    {
        // 1. Space-separated — trust the source
        if (native.Contains(' ') || native.Contains('　'))
        {
            var spaceParts = native.Split([' ', '　'], StringSplitOptions.RemoveEmptyEntries);
            if (spaceParts.Length >= 2)
                return (spaceParts[0], string.Join("", spaceParts[1..]));
        }

        // 2. Romaji hints available — use kana length estimation
        if (!string.IsNullOrWhiteSpace(firstHint) && !string.IsNullOrWhiteSpace(lastHint))
        {
            var result = SplitWithRomajiHints(native, firstHint, lastHint);
            if (result != null) return result;
        }

        // 3. Heuristic fallback
        return SplitByHeuristic(native);
    }

    private static (string family, string given)? SplitWithRomajiHints(
        string native, string firstRomaji, string lastRomaji)
    {
        var cleaned = native.Replace(" ", "").Replace("　", "");
        if (cleaned.Length < 2) return null;

        int givenKanaLen;
        int familyKanaLen;
        try
        {
            givenKanaLen = WanaKana.ToHiragana(firstRomaji.Trim()).Length;
            familyKanaLen = WanaKana.ToHiragana(lastRomaji.Trim()).Length;
        }
        catch
        {
            return null;
        }

        if (givenKanaLen == 0 || familyKanaLen == 0) return null;

        int bestSplit = -1;
        double bestScore = double.MaxValue;

        for (int i = 1; i < cleaned.Length; i++)
        {
            double familyRatio = (double)familyKanaLen / i;
            double givenRatio = (double)givenKanaLen / (cleaned.Length - i);
            double score = Math.Abs(familyRatio - givenRatio);

            if (score < bestScore)
            {
                bestScore = score;
                bestSplit = i;
            }
        }

        if (bestSplit <= 0 || bestSplit >= cleaned.Length) return null;

        return (cleaned[..bestSplit], cleaned[bestSplit..]);
    }

    private static (string family, string given)? SplitByHeuristic(string name)
    {
        var cleaned = name.Replace(" ", "").Replace("　", "");
        if (cleaned.Length < 2) return null;

        // Script transition: find kanji→kana or kana→kanji boundary
        for (int i = 1; i < cleaned.Length; i++)
        {
            bool prevIsKanji = IsKanjiChar(cleaned[i - 1]);
            bool currIsKanji = IsKanjiChar(cleaned[i]);
            bool prevIsKana = JapaneseTextHelper.IsKana(cleaned[i - 1]);
            bool currIsKana = JapaneseTextHelper.IsKana(cleaned[i]);

            if ((prevIsKanji && currIsKana) || (prevIsKana && currIsKanji))
                return (cleaned[..i], cleaned[i..]);
        }

        // All-kanji heuristics
        if (cleaned.All(IsKanjiChar))
        {
            if (cleaned.Length == 4)
                return (cleaned[..2], cleaned[2..]);
            if (cleaned.Length == 3)
                return (cleaned[..2], cleaned[2..]);
        }

        return null;
    }

    private static bool IsKanjiChar(char c) => JapaneseTextHelper.IsKanji(c);
}
