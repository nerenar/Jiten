using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using WanaKanaShaapu;

namespace Jiten.Parser.Scoring;

internal static class FormCandidateScorer
{
    public static FormScoreTrace Score(
        FormCandidate candidate,
        FormScoringContext context,
        IReadOnlySet<string> archaicPosTypes)
    {
        int wordScore = WordPriorityScorer.Score(candidate, context.IsNameContext, archaicPosTypes);
        int entryPriorityScore = EntryPriorityScorer.Score(candidate);
        int formPriorityScore = FormPriorityScorer.Score(candidate, context.IsKanaSurface);
        int formFlagScore = FormFlagScorer.Score(candidate, context);

        int surfaceMatchScore = SurfaceScorer.Score(candidate, context);
        surfaceMatchScore += LemmaScorer.Score(candidate, context, surfaceMatchScore);

        bool conjugatedIdentityPenaltyApplied =
            PenaltyScorer.ApplyConjugatedIdentityPenalty(candidate, context, ref surfaceMatchScore);
        PenaltyScorer.ApplyExpressionConflictPenalty(candidate, context, ref surfaceMatchScore);

        int scriptScore = ScriptScorer.Score(candidate, context);
        int readingMatchScore = ReadingScorer.Score(candidate, context, conjugatedIdentityPenaltyApplied);

        return new FormScoreTrace(
                                  wordScore,
                                  entryPriorityScore,
                                  formPriorityScore,
                                  formFlagScore,
                                  surfaceMatchScore,
                                  scriptScore,
                                  readingMatchScore,
                                  conjugatedIdentityPenaltyApplied);
    }
}

internal static class WordPriorityScorer
{
    public static int Score(FormCandidate candidate, bool isNameContext, IReadOnlySet<string> archaicPosTypes)
    {
        var word = candidate.Word;
        int wordScore = 0;

        if (word.Priorities?.Contains("jiten") == true)
            wordScore += 100;

        if (!isNameContext && word.PartsOfSpeech.ToPartOfSpeech().Contains(PartOfSpeech.Name))
            wordScore -= 50;

        if (word.PartsOfSpeech.Contains("arch"))
        {
            bool hasFrequencyMarker = word.Priorities?.Any(p =>
                                                               p is "ichi1" or "ichi2" or "news1" or "news2" or "jiten" ||
                                                               p.StartsWith("nf")) == true;
            bool hasNonArchaicPos = word.PartsOfSpeech.Any(p =>
                p is not "arch" and not "exp" and not "uk" and not "on-mim"
                && (p.StartsWith('v') && p is not "vulg" and not "vet" and not "vidg"
                    || p is "adj-i" or "adj-ix" or "adj-na" or "adj-pn" or "adj-no"
                    or "n" or "n-suf" or "n-pref" or "adv" or "int" or "conj" or "pn" or "ctr"));
            if (!hasFrequencyMarker && !hasNonArchaicPos)
                wordScore -= 350;
        }

        if (word.PartsOfSpeech.Any(archaicPosTypes.Contains))
            wordScore -= 75;

        if (word.PartsOfSpeech.Any(p => p is "on-mim"))
            wordScore += 10;

        // Shorter deconjugation chains are preferred for morphological plausibility.
        int chainCount = candidate.DeconjForm?.Process.Count ?? 0;
        if (chainCount <= 2)
            wordScore += 8;
        else
            wordScore -= 8 * (chainCount - 2);

        return wordScore;
    }
}

internal static class EntryPriorityScorer
{
    public static int Score(FormCandidate candidate)
    {
        var wordPri = candidate.Word.Priorities ?? [];
        int entryPriorityScore = 0;

        if (wordPri.Contains("ichi1")) entryPriorityScore += 20;
        if (wordPri.Contains("ichi2")) entryPriorityScore += 10;
        if (wordPri.Contains("news1")) entryPriorityScore += 15;
        if (wordPri.Contains("news2")) entryPriorityScore += 10;
        if (wordPri.Contains("gai1")) entryPriorityScore += 15;
        if (wordPri.Contains("gai2")) entryPriorityScore += 10;

        var wnf = wordPri.FirstOrDefault(p => p.StartsWith("nf"));
        if (wnf is { Length: > 2 } && int.TryParse(wnf[2..], out var wnfRank))
        {
            entryPriorityScore += Math.Max(0, 5 - (int)Math.Round(wnfRank / 10f));
            if (wnfRank <= 5)
                entryPriorityScore += 6 - wnfRank;
        }

        if (entryPriorityScore == 0)
        {
            if (wordPri.Contains("spec1")) entryPriorityScore += 15;
            if (wordPri.Contains("spec2")) entryPriorityScore += 5;
        }
        else
        {
            if (wordPri.Contains("spec1")) entryPriorityScore += 4;
            if (wordPri.Contains("spec2")) entryPriorityScore += 2;
        }

        return entryPriorityScore;
    }
}

internal static class FormPriorityScorer
{
    public static int Score(FormCandidate candidate, bool isKanaSurface)
    {
        var word = candidate.Word;
        var priorities = candidate.Form.Priorities ?? [];
        var wordPri = word.Priorities ?? [];
        int formPriorityScore = 0;

        if (priorities.Contains("ichi1")) formPriorityScore += 10;
        if (priorities.Contains("ichi2")) formPriorityScore += 5;
        if (priorities.Contains("news1")) formPriorityScore += 8;
        if (priorities.Contains("news2")) formPriorityScore += 5;
        if (priorities.Contains("gai1")) formPriorityScore += 8;
        if (priorities.Contains("gai2")) formPriorityScore += 5;

        var nf = priorities.FirstOrDefault(p => p.StartsWith("nf"));
        if (nf != null && nf.Length > 2 && int.TryParse(nf[2..], out var nfRank))
            formPriorityScore += Math.Max(0, 3 - (int)Math.Round(nfRank / 10f));

        if (formPriorityScore == 0)
        {
            if (priorities.Contains("spec1")) formPriorityScore += 8;
            if (priorities.Contains("spec2")) formPriorityScore += 3;
        }

        // "uk" (usually-kana) bias, scaled by whether the word has stronger frequency evidence.
        if (word.PartsOfSpeech.Contains("uk"))
        {
            bool hasFreqMarker = wordPri.Any(p =>
                                                 p is "ichi1" or "ichi2" or "news1" or "news2" || p.StartsWith("nf"));
            int ukBonus = hasFreqMarker ? 10 : 3;
            if (candidate.Form.FormType == JmDictFormType.KanaForm || isKanaSurface)
                formPriorityScore += ukBonus;
            else
                formPriorityScore -= ukBonus;
        }

        return formPriorityScore;
    }
}

internal static class FormFlagScorer
{
    public static int Score(FormCandidate candidate, FormScoringContext context)
    {
        var word = candidate.Word;
        var form = candidate.Form;
        int formFlagScore = 0;

        bool formMatchesSurface = context.Surface == form.Text
                                  || (candidate.DeconjForm != null && candidate.DeconjForm.Text == form.Text);
        if (form.IsSearchOnly)
            formFlagScore += formMatchesSurface ? 0 : -300;
        if (form.IsObsolete)
            formFlagScore += formMatchesSurface ? 0 : -150;
        if (!form.IsActiveInLatestSource)
            formFlagScore -= 30;

        bool isPureKanaWord = word.Forms.All(f => f.FormType != JmDictFormType.KanjiForm);
        if (isPureKanaWord && form.FormType == JmDictFormType.KanaForm && context.IsKanaSurface)
            formFlagScore += 20;

        return formFlagScore;
    }
}

internal static class SurfaceScorer
{
    public static int Score(FormCandidate candidate, FormScoringContext context)
    {
        var surface = context.Surface;
        var formText = candidate.Form.Text;
        int score = 0;

        var surfaceHira = KanaScoringHelpers.ToNormalizedHiragana(surface, convertLongVowelMark: false);
        var formHira = KanaScoringHelpers.ToNormalizedHiragana(formText, convertLongVowelMark: false);

        if (surface == formText)
        {
            score += 300;
        }
        else if (surfaceHira == formHira)
        {
            score += KanaScoringHelpers.IsPureKanaScriptDifference(surface, formText) ? 280 : 120;
        }
        else
        {
            // Loose normalisation match (long vowel mark / small-tsu handling).
            var surfaceLoose = KanaScoringHelpers.ToNormalizedHiragana(surface, convertLongVowelMark: true);
            var formLoose = KanaScoringHelpers.ToNormalizedHiragana(formText, convertLongVowelMark: true);
            if (surfaceLoose == formLoose)
                score += 60;
        }

        return score;
    }
}

internal static class LemmaScorer
{
    public static int Score(FormCandidate candidate, FormScoringContext context, int existingSurfaceScore)
    {
        int score = 0;
        var word = candidate.Word;
        var formText = candidate.Form.Text;
        var formHira = KanaScoringHelpers.ToNormalizedHiragana(formText, convertLongVowelMark: false);

        var surface = context.Surface;
        var dictionaryForm = context.DictionaryForm;
        var normalizedForm = context.NormalizedForm;

        double lemmaScale = 1.0;
        if (candidate.DeconjForm?.Process is { Count: > 0 } deconjProcess)
            lemmaScale = Math.Max(0.0, 1.0 - (deconjProcess.Count - 1) * 0.35);

        // Lemma match — only when dictionaryForm differs from surface.
        if (!string.IsNullOrEmpty(dictionaryForm) && dictionaryForm != surface)
        {
            if (dictionaryForm == formText)
            {
                // Exact Sudachi DictionaryForm match keeps a floor for deep chains.
                double effectiveScale = Math.Max(0.3, lemmaScale);
                score += (int)(100 * effectiveScale);
            }
            else
            {
                var dictHira = KanaScoringHelpers.ToNormalizedHiragana(dictionaryForm, convertLongVowelMark: false);
                if (dictHira == formHira)
                    score += (int)(40 * lemmaScale);
            }
        }

        // NormalizedForm bonus — only when it differs from both surface and dictionaryForm.
        if (!string.IsNullOrEmpty(normalizedForm) && normalizedForm != surface && normalizedForm != dictionaryForm)
        {
            if (normalizedForm == formText)
            {
                score += (int)(50 * lemmaScale);
            }
            else
            {
                var normHira = KanaScoringHelpers.ToNormalizedHiragana(normalizedForm, convertLongVowelMark: false);
                if (normHira == formHira)
                    score += (int)(20 * lemmaScale);

                // Sudachi normalized form matches a form of this word (e.g. リス → 栗鼠).
                if (word.Forms.Any(f => f.Text == normalizedForm))
                    score += (int)(50 * lemmaScale);
            }
        }

        // Deconjugation-based lemma fallback when standard lemma evidence is absent.
        if (candidate.DeconjForm?.Text != null && existingSurfaceScore + score == 0)
        {
            var deconjHira = KanaScoringHelpers.ToNormalizedHiragana(
                                                                     candidate.DeconjForm.Text,
                                                                     convertLongVowelMark: false);

            if (deconjHira == formHira)
                score += (int)(100 * lemmaScale);
        }

        return score;
    }
}

internal static class PenaltyScorer
{
    public static bool ApplyConjugatedIdentityPenalty(
        FormCandidate candidate,
        FormScoringContext context,
        ref int surfaceMatchScore)
    {
        if (!string.IsNullOrEmpty(context.DictionaryForm)
            && context.DictionaryForm != context.Surface
            && context.Surface == candidate.Form.Text
            && (candidate.DeconjForm == null || candidate.DeconjForm.Process.Count == 0))
        {
            // Non-inflectable words (adj-pn, etc.) cannot be conjugated forms,
            // so the penalty should not apply (e.g. 亡き is adj-pn, not a conjugation of 亡い)
            bool isInflectable = candidate.Word.PartsOfSpeech.Any(p =>
                p is "adj-i" or "adj-ix"
                || (p.StartsWith('v') && p is not "vulg" and not "vet" and not "vidg"));

            if (!isInflectable)
                return false;

            surfaceMatchScore -= 200;
            return true;
        }

        return false;
    }

    public static void ApplyExpressionConflictPenalty(
        FormCandidate candidate,
        FormScoringContext context,
        ref int surfaceMatchScore)
    {
        if (string.IsNullOrEmpty(context.DictionaryForm)
            || context.DictionaryForm == context.Surface
            || context.Surface != candidate.Form.Text)
            return;

        var pos = candidate.Word.PartsOfSpeech;
        bool isExpressionOnly = pos.All(p => p is "exp" or "on-mim");
        if (!isExpressionOnly)
            return;

        bool hasFreqMarker = candidate.Word.Priorities?.Any(p =>
            p is "ichi1" or "ichi2" or "news1" or "news2" or "jiten" || p.StartsWith("nf")) == true;
        if (hasFreqMarker)
            return;

        bool dictFormMatchesWord = candidate.Word.Forms.Any(f => f.Text == context.DictionaryForm);
        if (!dictFormMatchesWord)
            surfaceMatchScore -= 250;
    }
}

internal static class ScriptScorer
{
    public static int Score(FormCandidate candidate, FormScoringContext context)
    {
        var word = candidate.Word;
        var form = candidate.Form;
        var surface = context.Surface;

        int scriptScore = 5 * KanaScoringHelpers.GetCommonPrefixLen(surface, form.Text);

        // Ichidan stem bonus — suppress when Sudachi identifies a different verb.
        if (form.Text.Length > 2
            && form.Text[^1] == 'る'
            && word.PartsOfSpeech.Any(p => p is "v1" or "v1-s")
            && surface.StartsWith(form.Text[..^1], StringComparison.Ordinal))
        {
            bool dictFormConflicts = context.DictionaryForm is not null
                                     && context.DictionaryForm != surface
                                     && !word.Forms.Any(f => f.Text == context.DictionaryForm);

            if (!dictFormConflicts)
            {
                int stemLen = form.Text.Length - 1;
                scriptScore += Math.Min(15, stemLen * 5);
            }
        }

        return scriptScore;
    }
}

internal static class ReadingScorer
{
    public static int Score(FormCandidate candidate, FormScoringContext context, bool conjugatedIdentityPenaltyApplied)
    {
        int readingMatchScore = 0;
        if (!string.IsNullOrEmpty(context.SudachiReading) && !context.IsKanaSurface)
        {
            var word = candidate.Word;
            var sudachiHira = KanaScoringHelpers.ToNormalizedHiragana(context.SudachiReading, convertLongVowelMark: false);

            bool hasMatchingReading = word.Forms
                                          .Where(f => f.FormType == JmDictFormType.KanaForm)
                                          .Any(f => KanaScoringHelpers.ToNormalizedHiragana(
                                                                                            f.Text,
                                                                                            convertLongVowelMark: false) == sudachiHira);
            if (hasMatchingReading)
            {
                readingMatchScore += 50;
            }
            // Prefix match — for ichidan conjugated stems.
            else if (sudachiHira.Length > 1
                     && word.Forms
                            .Where(f => f.FormType == JmDictFormType.KanaForm)
                            .Any(f =>
                            {
                                var hiragana = KanaScoringHelpers.ToNormalizedHiragana(f.Text, convertLongVowelMark: false);
                                return hiragana.Length > sudachiHira.Length
                                       && hiragana.StartsWith(sudachiHira, StringComparison.Ordinal);
                            }))
            {
                readingMatchScore += 50;
            }
            // Stem fallback — for godan conjugated stems.
            else if (sudachiHira.Length > 1)
            {
                var sudachiStem = sudachiHira[..^1];
                bool hasStemMatch = word.Forms
                                        .Where(f => f.FormType == JmDictFormType.KanaForm)
                                        .Any(f =>
                                        {
                                            var hiragana = KanaScoringHelpers.ToNormalizedHiragana(
                                             f.Text,
                                             convertLongVowelMark: false);

                                            return hiragana.Length > 1 && hiragana[..^1] == sudachiStem;
                                        });
                if (hasStemMatch)
                    readingMatchScore += 25;
            }
        }

        if (conjugatedIdentityPenaltyApplied)
            readingMatchScore = 0;

        return readingMatchScore;
    }
}

internal static class KanaScoringHelpers
{
    private static readonly DefaultOptions NoLongVowelConversion = new() { ConvertLongVowelMark = false };
    private static readonly DefaultOptions LooseLongVowelConversion = new() { ConvertLongVowelMark = true };

    public static string ToNormalizedHiragana(string text, bool convertLongVowelMark)
    {
        var options = convertLongVowelMark ? LooseLongVowelConversion : NoLongVowelConversion;
        return KanaNormalizer.Normalize(WanaKana.ToHiragana(text, options));
    }

    public static bool IsPureKanaScriptDifference(string a, string b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i]) continue;
            int diff = a[i] - b[i];
            if (diff is not 0x60 and not -0x60) return false;
        }
        return true;
    }

    public static int GetCommonPrefixLen(string s1, string s2)
    {
        int len = Math.Min(s1.Length, s2.Length);
        int match = 0;
        for (int i = 0; i < len; i++)
        {
            if (s1[i] == s2[i])
                match++;
            else
                break;
        }

        return match;
    }
}