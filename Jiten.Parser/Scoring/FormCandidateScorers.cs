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
        int wordScore = WordPriorityScorer.Score(candidate, context.IsNameContext, context.IsArchaicSentence, archaicPosTypes, context.IsSentenceInitial);
        int entryPriorityScore = EntryPriorityScorer.Score(candidate);
        int formPriorityScore = FormPriorityScorer.Score(candidate, context.IsKanaSurface);
        int formFlagScore = FormFlagScorer.Score(candidate, context);

        int surfaceMatchScore = SurfaceScorer.Score(candidate, context);
        surfaceMatchScore += LemmaScorer.Score(candidate, context, surfaceMatchScore);

        bool conjugatedIdentityPenaltyApplied =
            PenaltyScorer.ApplyConjugatedIdentityPenalty(candidate, context, ref surfaceMatchScore);
        bool expressionConflictPenaltyApplied =
            PenaltyScorer.ApplyExpressionConflictPenalty(candidate, context, ref surfaceMatchScore);

        int scriptScore = ScriptScorer.Score(candidate, context);
        int readingMatchScore = ReadingScorer.Score(candidate, context,
            conjugatedIdentityPenaltyApplied || expressionConflictPenaltyApplied);

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
    public static int Score(FormCandidate candidate, bool isNameContext, bool isArchaicSentence, IReadOnlySet<string> archaicPosTypes, bool isSentenceInitial = false)
    {
        var word = candidate.Word;
        int wordScore = 0;

        if (word.Priorities?.Contains("jiten") == true)
            wordScore += 100;

        if (!isNameContext && word.PartsOfSpeech.ToPartOfSpeech().Contains(PartOfSpeech.Name))
            wordScore -= 50;

        if (word.IsFullyArchaic)
        {
            bool hasFrequencyMarker = word.Priorities?.Any(p =>
                                                               p is "ichi1" or "ichi2" or "news1" or "news2" or "jiten" ||
                                                               p.StartsWith("nf")) == true;
            if (!hasFrequencyMarker)
                wordScore -= isArchaicSentence ? 50 : 350;
        }

        // Only penalise when the word has NO non-archaic primary POS.
        // E.g. 無し has adj-ku (archaic) BUT also n — modern usage still valid, skip penalty.
        // "suf"/"pref" are sub-categorisations, not primary word classes — a word can be
        // simultaneously archaic (v2a-s) and a suffix, so they must not exempt it from the penalty.
        var readingPos = ReadingPosHelper.GetPosForReading(word, candidate.ReadingIndex);
        IEnumerable<string> posToCheck = readingPos.Count > 0 ? readingPos : word.PartsOfSpeech;
        if (posToCheck.Any(archaicPosTypes.Contains)
            && !posToCheck.Any(p => p is "n" or "n-adv" or "n-t" or "n-pref" or "n-suf"
                                     or "v1" or "v1-s"
                                     or "v5a" or "v5b" or "v5g" or "v5k" or "v5k-s" or "v5m"
                                     or "v5n" or "v5r" or "v5r-i" or "v5s" or "v5t"
                                     or "v5u" or "v5u-s" or "v5uru"
                                     or "vs" or "vs-c" or "vs-i" or "vs-s" or "vk" or "vz"
                                     or "adj-i" or "adj-ix" or "adj-na" or "adj-no" or "adj-pn"
                                     or "adv" or "adv-to" or "cop" or "aux" or "aux-v" or "aux-adj"
                                     or "prt" or "int" or "exp" or "pref" or "ctr"
                                     or "on-mim" or "pn" or "conj"))
            wordScore -= isArchaicSentence ? 15 : 75;

        if (word.PartsOfSpeech.Any(p => p is "on-mim"))
            wordScore += 10;

        // Adverbs commonly start clauses; mild boost when sentence-initial.
        if (isSentenceInitial && word.PartsOfSpeech.Any(p => p is "adv" or "adv-to"))
            wordScore += 10;

        // Unclass entries (JMnedict names with no category) are last-resort matches.
        // Penalise them when not in a name context so proper words score higher.
        if (!isNameContext && word.PartsOfSpeech.All(p => p is "unclass"))
            wordScore -= 40;

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

        // Grammatical copula words (である, だ, etc.) compete against high-frequency content words.
        // Boost them so they aren't crowded out by ichi1/news1 verbs in grammatical positions.
        if (candidate.Word.PartsOfSpeech.Contains("cop"))
            entryPriorityScore += 20;

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
        if (nf is { Length: > 2 } && int.TryParse(nf[2..], out var nfRank))
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

        // High-frequency kanji words (jiten priority) should not beat grammatical words
        // via a single-char kana match. Single-char kana tokens are virtually always
        // grammatical (copula/aux/particle); jiten content words like 打(だ) compete
        // unfairly through their kana reading and must be suppressed.
        if (!isPureKanaWord && form.FormType == JmDictFormType.KanaForm
            && context.IsKanaSurface && context.Surface.Length == 1
            && word.Priorities?.Contains("jiten") == true)
            formFlagScore -= 100;

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
            var readingPos = ReadingPosHelper.GetPosForReading(candidate.Word, candidate.ReadingIndex);
            IEnumerable<string> posToCheck = readingPos.Count > 0 ? readingPos : candidate.Word.PartsOfSpeech;
            bool isInflectable = posToCheck.Any(p =>
                p is "adj-i" or "adj-ix"
                || (p.StartsWith('v') && p is not "vulg" and not "vet" and not "vidg"));

            if (!isInflectable)
            {
                // Expressions have their own ExpressionConflictPenalty mechanism; skip this penalty for them.
                bool isExpression = posToCheck.Any(p => p is "exp" or "on-mim");
                if (isExpression) return false;

                // Particles are function words whose surface form IS their canonical form.
                // Sudachi may give DictionaryForm=だ for で (etymological), but で the particle
                // is not a conjugation — don't penalise it.
                bool isParticle = posToCheck.Any(p => p is "prt");
                if (isParticle) return false;

                // adj-pn/adj-t are standalone prenominal adjectives (e.g. 亡き, 無き, 堂々たる).
                // Their surface IS their dictionary form; Sudachi may give a different DictionaryForm
                // (the archaic base), but only skip the penalty when that DictForm IS one of this
                // word's own forms. If DictForm points to a completely different word
                // (e.g. させる→する), fall through to the nounHasDictForm check below.
                bool isAdnominal = posToCheck.Any(p => p is "adj-pn" or "adj-t");
                if (isAdnominal && candidate.Word.Forms.Any(f => f.Text == context.DictionaryForm))
                    return false;

                // For non-inflectable words (e.g., plain nouns), still apply the penalty when Sudachi's
                // DictionaryForm doesn't appear in this word's forms — it points to a different (inflectable) word.
                // E.g., surface=答え, DictForm=答える: noun 答え shouldn't beat verb 答える via surface match.
                bool nounHasDictForm = candidate.Word.Forms.Any(f => f.Text == context.DictionaryForm);
                if (!nounHasDictForm)
                {
                    surfaceMatchScore -= 200;
                    return true;
                }
                return false;
            }

            // adj-ix (e.g. いい/よい) has irregular conjugations; its base forms are not conjugated
            // forms of other words, even if Sudachi misidentifies them (e.g. いい as verb いう).
            if (posToCheck.Any(p => p is "adj-ix"))
                return false;

            surfaceMatchScore -= 200;
            return true;
        }

        return false;
    }

    public static bool ApplyExpressionConflictPenalty(
        FormCandidate candidate,
        FormScoringContext context,
        ref int surfaceMatchScore)
    {
        if (string.IsNullOrEmpty(context.DictionaryForm)
            || context.DictionaryForm == context.Surface
            || context.Surface != candidate.Form.Text)
            return false;

        var readingPos = ReadingPosHelper.GetPosForReading(candidate.Word, candidate.ReadingIndex);
        IEnumerable<string> posToCheck = readingPos.Count > 0 ? readingPos : candidate.Word.PartsOfSpeech;
        bool isExpressionOnly = posToCheck.All(p => p is "exp" or "on-mim");
        if (!isExpressionOnly)
            return false;

        bool hasFreqMarker = candidate.Word.Priorities?.Any(p =>
            p is "ichi1" or "ichi2" or "news1" or "news2" or "jiten" || p.StartsWith("nf")) == true;
        if (hasFreqMarker)
            return false;

        bool dictFormMatchesWord = candidate.Word.Forms.Any(f => f.Text == context.DictionaryForm);
        if (!dictFormMatchesWord)
        {
            surfaceMatchScore -= 250;
            return true;
        }

        return false;
    }
}

internal static class ScriptScorer
{
    // Superlinear prefix scoring: 5*n*(n+1)/2 capped at n=5 → 5, 15, 30, 50, 75
    private static readonly int[] KanjiScale = [0, 5, 15, 30, 50, 75];
    // Weaker scaling for kana-only forms: 3*n*(n+1)/2 capped at n=5 → 3, 9, 18, 30, 45
    private static readonly int[] KanaScale = [0, 3, 9, 18, 30, 45];

    public static int Score(FormCandidate candidate, FormScoringContext context)
    {
        var word = candidate.Word;
        var form = candidate.Form;
        var surface = context.Surface;

        int prefixLen = KanaScoringHelpers.GetCommonPrefixLen(surface, form.Text);
        bool hasKanji = KanaScoringHelpers.ContainsKanji(form.Text);
        var scale = hasKanji ? KanjiScale : KanaScale;
        int scriptScore = scale[Math.Min(prefixLen, scale.Length - 1)];

        // Ichidan stem bonus — suppress when Sudachi identifies a different verb.
        var ichidanReadingPos = ReadingPosHelper.GetPosForReading(word, candidate.ReadingIndex);
        IEnumerable<string> ichidanPosToCheck = ichidanReadingPos.Count > 0 ? ichidanReadingPos : word.PartsOfSpeech;
        if (form.Text.Length > 2
            && form.Text[^1] == 'る'
            && ichidanPosToCheck.Any(p => p is "v1" or "v1-s")
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
            // Reverse prefix match — conjugated reading starts with an ichidan verb's kana stem.
            // E.g., Sudachi reading "できません" starts with "でき" (stem of できる).
            // Restricted to forms ending in る (ichidan verbs) to avoid false positives with godan verbs
            // (e.g., かまう stem "かま" falsely matching "かまえ").
            else if (sudachiHira.Length > 2
                     && word.Forms
                            .Where(f => f.FormType == JmDictFormType.KanaForm)
                            .Any(f =>
                            {
                                var hiragana = KanaScoringHelpers.ToNormalizedHiragana(f.Text, convertLongVowelMark: false);
                                if (hiragana.Length < 3 || !hiragana.EndsWith('る')) return false;
                                var formStem = hiragana[..^1];
                                return sudachiHira.StartsWith(formStem, StringComparison.Ordinal);
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

internal static class ReadingPosHelper
{
    public static HashSet<string> GetPosForReading(JmDictWord word, byte readingIndex)
    {
        if (word.Definitions.Count == 0)
            return [];

        return word.Definitions
            .Where(d => d.RestrictedToReadingIndices == null
                     || d.RestrictedToReadingIndices.Contains((short)readingIndex))
            .SelectMany(d => d.PartsOfSpeech)
            .ToHashSet();
    }
}

internal static class KanaScoringHelpers
{
    public static string ToNormalizedHiragana(string text, bool convertLongVowelMark)
    {
        return KanaNormalizer.Normalize(KanaConverter.ToHiragana(text, convertLongVowelMark));
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

    public static bool ContainsKanji(string text)
    {
        foreach (char c in text)
        {
            if (c >= '\u4E00' && c <= '\u9FFF')
                return true;
        }

        return false;
    }
}