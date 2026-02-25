namespace Jiten.Parser.Resolution;

internal static class LookupCandidateCollector
{
    /// Collects distinct word IDs from lookups by raw text, hiragana, and optionally kana-normalized and long-vowel-stripped variants.
    public static List<int> CollectIds(
        Dictionary<string, List<int>> lookups,
        string text,
        bool includeKanaNormalized = true,
        bool includeLongVowelStripped = false)
    {
        var ids = new List<int>();
        if (lookups.TryGetValue(text, out var direct)) ids.AddRange(direct);

        var hiragana = KanaConverter.ToHiragana(text, convertLongVowelMark: false);
        if (hiragana != text && lookups.TryGetValue(hiragana, out var hiraIds))
            ids.AddRange(hiraIds);

        if (includeKanaNormalized)
        {
            var normalized = KanaNormalizer.Normalize(hiragana);
            if (normalized != hiragana && lookups.TryGetValue(normalized, out var normIds))
                ids.AddRange(normIds);
        }

        if (includeLongVowelStripped && text.Contains('ー'))
        {
            var textStripped = text.Replace("ー", "");
            bool endsInBar = text.EndsWith("ー");
            bool isShort = textStripped.Length <= 2;

            if (!endsInBar || !isShort)
            {
                var strippedHira = KanaConverter.ToHiragana(textStripped);
                if (lookups.TryGetValue(strippedHira, out var stripIds))
                    ids.AddRange(stripIds);
            }
        }

        return ids.Distinct().ToList();
    }
}
