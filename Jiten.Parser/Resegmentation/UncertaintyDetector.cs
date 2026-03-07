using Jiten.Core.Data;

namespace Jiten.Parser.Resegmentation;

internal static class UncertaintyDetector
{
    internal static readonly PartOfSpeech[] SkipPos =
    [
        PartOfSpeech.Particle, PartOfSpeech.Auxiliary, PartOfSpeech.Verb, PartOfSpeech.IAdjective,
        PartOfSpeech.SupplementarySymbol, PartOfSpeech.Symbol, PartOfSpeech.Conjunction,
        PartOfSpeech.Adnominal, PartOfSpeech.Prefix, PartOfSpeech.BlankSpace,
        PartOfSpeech.Suffix, PartOfSpeech.NounSuffix,
        PartOfSpeech.Counter, PartOfSpeech.Numeral, PartOfSpeech.Filler,
        PartOfSpeech.Expression
    ];

    public static List<UncertainSpan> FindSpans(SentenceInfo sentence, Dictionary<string, List<int>> lookups)
    {
        var result = new List<UncertainSpan>();

        for (int i = 0; i < sentence.Words.Count; i++)
        {
            var (word, position, length) = sentence.Words[i];

            if (word.Text.Length < 3 || word.Text.Length > 14)
                continue;
            if (word.PreMatchedWordId != null)
                continue;
            if (Array.IndexOf(SkipPos, word.PartOfSpeech) >= 0)
                continue;

            if (HasMatch(word.Text, lookups))
                continue;
            if (word.DictionaryForm != word.Text && !string.IsNullOrEmpty(word.DictionaryForm) &&
                HasMatch(word.DictionaryForm, lookups))
                continue;

            result.Add(new UncertainSpan
            {
                WordIndex = i,
                Text      = word.Text,
                Position  = position,
                Length    = length
            });
        }

        return result;
    }

    internal static bool HasMatch(string text, Dictionary<string, List<int>> lookups)
    {
        if (lookups.TryGetValue(text, out var ids) && ids.Count > 0) return true;
        try
        {
            var hira = KanaNormalizer.Normalize(KanaConverter.ToHiragana(text, convertLongVowelMark: false));
            if (hira != text && lookups.TryGetValue(hira, out ids) && ids.Count > 0) return true;

            if (text.Contains('ー'))
            {
                var stripped = text.Replace("ー", "");
                if (stripped.Length > 0)
                {
                    var strippedHira = KanaNormalizer.Normalize(KanaConverter.ToHiragana(stripped, convertLongVowelMark: false));
                    if (lookups.TryGetValue(strippedHira, out ids) && ids.Count > 0) return true;
                }
            }

            if (HasGodanDictFormMatch(text, lookups) || (hira != text && HasGodanDictFormMatch(hira, lookups)))
                return true;
        }
        catch { }
        return false;
    }

    private static bool HasGodanDictFormMatch(string text, Dictionary<string, List<int>> lookups)
    {
        var dictForm = MorphologicalAnalyser.TryGodanDictForm(text);
        return dictForm != null && lookups.TryGetValue(dictForm, out var ids) && ids.Count > 0;
    }
}
