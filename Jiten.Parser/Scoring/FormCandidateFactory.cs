using Jiten.Core.Data.JMDict;

namespace Jiten.Parser.Scoring;

internal static class FormCandidateFactory
{
    public static List<FormCandidate> EnumerateCandidateForms(
        JmDictWord word,
        string targetHiragana,
        bool allowLooseLvmMatch,
        DeconjugationForm? deconjForm = null,
        string? surface = null)
    {
        var candidates = new List<FormCandidate>();
        var targetNormalized = KanaNormalizer.Normalize(targetHiragana);

        foreach (var form in word.Forms)
        {
            if (form.ReadingIndex > 255)
                continue;

            var formHiragana = KanaConverter.ToHiragana(form.Text, convertLongVowelMark: false);
            var formNormalized = KanaNormalizer.Normalize(formHiragana);

            bool phoneticMatch = formNormalized == targetNormalized;

            if (!phoneticMatch && allowLooseLvmMatch)
            {
                var targetLoose = KanaNormalizer.Normalize(KanaConverter.ToHiragana(targetHiragana));
                var formLoose = KanaNormalizer.Normalize(KanaConverter.ToHiragana(form.Text));
                phoneticMatch = formLoose == targetLoose;

                if (!phoneticMatch && targetHiragana.Contains('ー'))
                {
                    var targetStripped = targetHiragana.Replace("ー", "");
                    var formStripped = formHiragana.Replace("ー", "");
                    phoneticMatch = targetStripped.Length > 0 && targetStripped == formStripped;
                }
            }

            if (!phoneticMatch)
                continue;

            candidates.Add(new FormCandidate(word, form, (byte)form.ReadingIndex, targetHiragana, deconjForm));
        }

        // Per-word hard filters: drop search-only/obsolete forms only if non-search-only/non-obsolete alternatives exist
        if (candidates.Count <= 1)
            return candidates;

        var nonSearchOnly = candidates.Where(c => !c.Form.IsSearchOnly || c.Form.Text == surface).ToList();
        if (nonSearchOnly.Count > 0)
            candidates = nonSearchOnly;

        var nonObsolete = candidates.Where(c => !c.Form.IsObsolete || c.Form.Text == surface).ToList();
        if (nonObsolete.Count > 0)
            candidates = nonObsolete;

        return candidates;
    }

    public static bool HasKanaReadingMatch(JmDictWord word, string sudachiReading, bool allowStemMatch = false)
    {
        var readingHiragana = KanaConverter.ToHiragana(sudachiReading, convertLongVowelMark: false);
        return word.Forms
            .Where(f => f.FormType == JmDictFormType.KanaForm)
            .Any(f =>
            {
                var formHiragana = KanaConverter.ToHiragana(f.Text, convertLongVowelMark: false);
                return formHiragana == readingHiragana ||
                       (allowStemMatch && formHiragana.StartsWith(readingHiragana));
            });
    }

    public static bool IsSuruNounWithoutExpression(IList<string> posTags)
    {
        bool hasNoun = false, hasSuru = false, hasExpression = false;
        foreach (var tag in posTags)
        {
            if (tag is "n" or "n-adv" or "n-t") hasNoun = true;
            else if (tag is "vs" or "vs-s" or "vs-i" or "vs-c") hasSuru = true;
            else if (tag is "exp") hasExpression = true;
        }

        return hasNoun && hasSuru && !hasExpression;
    }
}
