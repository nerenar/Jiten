using Jiten.Core.Data;
using Jiten.Parser.Resolution;

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

    public static List<UncertainSpan> FindSpans(SentenceInfo sentence, Dictionary<string, List<int>> lookups,
        HashSet<string>? protectedSurfaces = null)
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
            if (protectedSurfaces != null && protectedSurfaces.Contains(word.Text))
                continue;

            // Multi-character kanji numerals (五十七, 六十一) are OOV in Sudachi and often
            // only match name entries in JMDict. Flag them for resegmentation so the scorer
            // can evaluate splits like 五十+七 which resolve to real numeral entries.
            bool isCompoundNumeral = word.PartOfSpeechSection1 == PartOfSpeechSection.Numeral && word.Text.Length > 1;
            if (!isCompoundNumeral)
            {
                if (HasMatch(word.Text, lookups))
                    continue;
                if (word.DictionaryForm != word.Text && !string.IsNullOrEmpty(word.DictionaryForm) &&
                    HasMatch(word.DictionaryForm, lookups))
                    continue;
            }

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
        if (LookupCandidateCollector.HasAnyMatch(lookups, text, includeLongVowelStripped: true))
            return true;

        try
        {
            var hira = KanaConverter.ToNormalizedHiragana(text);

            if (HasGodanDictFormMatch(text, lookups) || (hira != text && HasGodanDictFormMatch(hira, lookups)))
                return true;

            if (HasIchidanDictFormMatch(text, lookups) || (hira != text && HasIchidanDictFormMatch(hira, lookups)))
                return true;

            if (HasAdjSaNominalizationMatch(text, lookups) || (hira != text && HasAdjSaNominalizationMatch(hira, lookups)))
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

    private static bool HasIchidanDictFormMatch(string text, Dictionary<string, List<int>> lookups)
    {
        if (text.Length < 2) return false;
        var dictForm = text + "る";
        return lookups.TryGetValue(dictForm, out var ids) && ids.Count > 0;
    }

    private static bool HasAdjSaNominalizationMatch(string text, Dictionary<string, List<int>> lookups)
    {
        if (text.Length < 3 || text[^1] != 'さ') return false;
        var adjForm = text[..^1] + "い";
        return lookups.TryGetValue(adjForm, out var ids) && ids.Count > 0;
    }
}
