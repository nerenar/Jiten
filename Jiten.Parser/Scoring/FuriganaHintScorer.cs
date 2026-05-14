using Jiten.Core.Data.JMDict;

namespace Jiten.Parser.Scoring;

internal static class FuriganaHintScorer
{
    public static int Score(FormCandidate candidate, string? hintHiragana)
    {
        if (hintHiragana is null) return 0;

        foreach (var form in candidate.Word.Forms)
        {
            if (form.FormType != JmDictFormType.KanaForm) continue;

            var formHiragana = KanaScoringHelpers.ToNormalizedHiragana(form.Text, convertLongVowelMark: false);
            if (formHiragana == hintHiragana)
                return 500;
        }

        return 0;
    }
}
