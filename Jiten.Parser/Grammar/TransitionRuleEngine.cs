using Jiten.Core.Data;
using Jiten.Parser;
using Jiten.Parser.Diagnostics;
using Jiten.Parser.Scoring;

namespace Jiten.Parser.Grammar;

internal static class TransitionRuleEngine
{
    // Two-pass approach
    //   Pass 1 — while-loop strips all leading verb-attaching auxiliaries
    //   Pass 2 — backwards loop validates aux context and counter placement
    internal static void ApplyHardRules(
        List<(WordInfo word, int pos, int len)> words,
        Func<string, bool> hasLookup,
        ParserDiagnostics? diagnostics = null)
    {
        var rules = TransitionRuleSets.HardRules;

        // Pass 1: strip all sentence-initial tokens that can never begin a clause (needs while loop:
        // removing index 0 exposes a new index 0 that also needs to be checked)
        var leadingStripRules = Array.FindAll(rules,
            r => r.Id is "leading-aux-strip" or "particle-at-sentence-start");
        bool leadingRemoved;
        do
        {
            leadingRemoved = false;
            if (words.Count == 0) break;
            var window = BuildWindow(words, 0);
            foreach (var rule in leadingStripRules)
            {
                if (!MatchesAll(window, rule.WhenToken)) continue;
                if (IsValidState(window, rule.ValidIf)) continue;
                diagnostics?.LogTransitionViolation(rule.Id, window);
                words.RemoveAt(0);
                leadingRemoved = true;
                break;
            }
        } while (leadingRemoved);

        // Pass 2: backwards pass for context-dependent rules (aux following wrong POS, orphaned counters)
        for (int i = words.Count - 1; i >= 0; i--)
        {
            var window = BuildWindow(words, i);
            foreach (var rule in rules)
            {
                if (rule.Id is "leading-aux-strip" or "particle-at-sentence-start") continue;
                if (!MatchesAll(window, rule.WhenToken)) continue;
                if (IsValidState(window, rule.ValidIf)) continue;

                diagnostics?.LogTransitionViolation(rule.Id, window);
                ApplyViolation(rule, words, i, hasLookup);
                break;
            }
        }
    }

    // ValidIf semantics: empty means "never valid" (always a violation when WhenToken matches).
    // Non-empty: valid only when ALL conditions match.
    private static bool IsValidState(TokenWindow w, MatchCondition[] validIf)
    {
        if (validIf.Length == 0) return false;
        return MatchesAll(w, validIf);
    }

    private static bool MatchesAll(TokenWindow w, MatchCondition[] conditions)
    {
        foreach (var c in conditions)
        {
            bool ok = c switch
            {
                MatchCondition.IsVerbOnlyAux =>
                    w.Current.PartOfSpeech == PartOfSpeech.Auxiliary &&
                    TransitionRuleSets.VerbOnlyAuxDictForms.Contains(w.Current.DictionaryForm),

                MatchCondition.IsVerbOrAdjAux =>
                    w.Current.PartOfSpeech == PartOfSpeech.Auxiliary &&
                    TransitionRuleSets.VerbOrAdjAuxDictForms.Contains(w.Current.DictionaryForm),

                MatchCondition.IsVerbAttachingAux =>
                    w.Current.PartOfSpeech == PartOfSpeech.Auxiliary &&
                    (TransitionRuleSets.VerbOnlyAuxDictForms.Contains(w.Current.DictionaryForm) ||
                     TransitionRuleSets.VerbOrAdjAuxDictForms.Contains(w.Current.DictionaryForm)),

                MatchCondition.IsAuxiliary =>
                    w.Current.PartOfSpeech == PartOfSpeech.Auxiliary,

                MatchCondition.IsCounter =>
                    w.Current.PartOfSpeech == PartOfSpeech.Counter ||
                    (w.Current.PartOfSpeech == PartOfSpeech.Suffix &&
                     w.Current.HasPartOfSpeechSection(PartOfSpeechSection.Counter)),

                MatchCondition.IsSentenceInitial =>
                    w.Index == 0,

                MatchCondition.PrevIsVerbOrAux =>
                    w.Prev?.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary,

                MatchCondition.PrevIsVerbAuxOrIAdj =>
                    w.Prev?.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary
                                         or PartOfSpeech.IAdjective,

                MatchCondition.PrevIsVerbAuxIAdjOrSfp =>
                    w.Prev?.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary
                                         or PartOfSpeech.IAdjective
                    || (w.Prev?.PartOfSpeech == PartOfSpeech.Particle &&
                        w.Prev?.HasPartOfSpeechSection(PartOfSpeechSection.SentenceEndingParticle) == true),

                MatchCondition.PrevIsNumericOrNoun =>
                    w.Prev?.PartOfSpeech is PartOfSpeech.Numeral or PartOfSpeech.Noun
                                         or PartOfSpeech.CommonNoun or PartOfSpeech.Pronoun
                                         or PartOfSpeech.Name,

                MatchCondition.PrevIsAuxiliary =>
                    w.Prev?.PartOfSpeech == PartOfSpeech.Auxiliary,

                MatchCondition.PrevExists =>
                    w.Prev != null,

                MatchCondition.IsSentenceEndingParticle =>
                    w.Current.PartOfSpeech == PartOfSpeech.Particle &&
                    w.Current.HasPartOfSpeechSection(PartOfSpeechSection.SentenceEndingParticle),

                MatchCondition.NextIsContentWord =>
                    w.Next?.PartOfSpeech is PartOfSpeech.Noun or PartOfSpeech.CommonNoun
                        or PartOfSpeech.Name or PartOfSpeech.Pronoun or PartOfSpeech.Verb
                        or PartOfSpeech.IAdjective or PartOfSpeech.NaAdjective
                        or PartOfSpeech.NominalAdjective or PartOfSpeech.Adverb
                        or PartOfSpeech.AdverbTo or PartOfSpeech.Numeral
                        or PartOfSpeech.PrenounAdjectival or PartOfSpeech.Counter
                        or PartOfSpeech.Prefix or PartOfSpeech.Expression,

                MatchCondition.IsPrefix =>
                    w.Current.PartOfSpeech == PartOfSpeech.Prefix,

                MatchCondition.IsSentenceFinal =>
                    w.Index == w.Count - 1,

                MatchCondition.NextIsParticle =>
                    w.Next?.PartOfSpeech == PartOfSpeech.Particle,

                MatchCondition.IsSuffix =>
                    w.Current.PartOfSpeech == PartOfSpeech.Suffix,

                MatchCondition.IsStrictCaseMarkingParticle =>
                    w.Current.PartOfSpeech == PartOfSpeech.Particle &&
                    TransitionRuleSets.StrictCaseMarkingParticles.Contains(w.Current.DictionaryForm),

                _ => false
            };
            if (!ok) return false;
        }
        return true;
    }

    private static void ApplyViolation(
        TransitionRule rule,
        List<(WordInfo word, int pos, int len)> words,
        int i,
        Func<string, bool> hasLookup)
    {
        switch (rule.OnViolation)
        {
            case ViolationAction.RemoveCurrent:
                words.RemoveAt(i);
                break;

            case ViolationAction.MergeWithPrevious:
                if (i == 0)
                {
                    words.RemoveAt(i);
                    break;
                }
                var (prevWord, prevPos, prevLen) = words[i - 1];
                var merged = prevWord.Text + words[i].word.Text;
                if (hasLookup(merged))
                {
                    var auxLen = words[i].len;
                    words[i - 1] = (new WordInfo(prevWord)
                    {
                        Text = merged,
                        DictionaryForm = merged,
                        NormalizedForm = merged,
                        PartOfSpeech = prevWord.PartOfSpeech
                    }, prevPos, prevLen + auxLen);
                    words.RemoveAt(i);
                }
                else
                {
                    words.RemoveAt(i);
                }
                break;

            case ViolationAction.ReclassifyCurrentAsNoun:
                words[i].word.PartOfSpeech = PartOfSpeech.Noun;
                break;
        }
    }

    private static bool IsSingleKanji(string? text) =>
        text is { Length: 1 } && text[0] >= '\u4E00' && text[0] <= '\u9FFF';

    private static TokenWindow BuildWindow(List<(WordInfo word, int pos, int len)> words, int i)
    {
        var prev = i > 0 ? words[i - 1].word : null;
        var next = i + 1 < words.Count ? words[i + 1].word : null;
        return new TokenWindow(prev, words[i].word, next, i, words.Count);
    }

    internal static (int bonus, List<string> rulesMatched) EvaluateSoftRules(ScoringWindow window)
    {
        int bonus = 0;
        var rulesMatched = new List<string>();
        var ctx = ConditionContext.FromScoringWindow(window);

        foreach (var rule in TransitionRuleSets.SoftRules)
        {
            if (!MatchesAll(ctx, rule.CandidateMatch)) continue;
            if (!MatchesAll(ctx, rule.ContextMatch)) continue;

            bonus += rule.Delta;
            rulesMatched.Add(rule.Id);
        }

        return (bonus, rulesMatched);
    }

    internal static bool HasApplicableSoftRules(ScoringWindow window)
    {
        var ctx = ConditionContext.FromScoringWindow(window);
        foreach (var rule in TransitionRuleSets.SoftRules)
        {
            if (!MatchesAll(ctx, rule.CandidateMatch)) continue;
            if (MatchesAll(ctx, rule.ContextMatch)) return true;
        }

        return false;
    }

    internal static bool CouldAnySoftRuleApply(
        List<PartOfSpeech> currentPOS, string currentText,
        List<PartOfSpeech>? prevPOS, string? prevText,
        List<PartOfSpeech>? nextPOS, string? nextText)
    {
        if (currentPOS.Count == 0) return false;

        var ctx = new ConditionContext(currentPOS, currentText, prevPOS, prevText, nextPOS, nextText);
        foreach (var rule in TransitionRuleSets.SoftRules)
        {
            if (!MatchesAll(ctx, rule.CandidateMatch)) continue;
            if (MatchesAll(ctx, rule.ContextMatch)) return true;
        }

        return false;
    }

    private readonly record struct ConditionContext(
        List<PartOfSpeech> CandidatePOS,
        string CandidateText,
        List<PartOfSpeech>? PrevPOS,
        string? PrevText,
        List<PartOfSpeech>? NextPOS,
        string? NextText,
        bool CandidateIsSuruNounVal = false)
    {
        public static ConditionContext FromScoringWindow(ScoringWindow w) => new(
            w.Candidate.Word.CachedPOS,
            w.Candidate.Form.Text,
            w.PrevResolvedPOS,
            w.PrevText,
            w.NextResolvedPOS,
            w.NextText,
            w.Candidate.Word.PartsOfSpeech.Any(p => p is "vs" or "vs-i" or "vs-s"));
    }

    private static bool MatchesAll(ConditionContext ctx, ScoringCondition[] conditions)
    {
        foreach (var c in conditions)
        {
            bool ok = c switch
            {
                ScoringCondition.CandidateIsNounLike =>
                    ctx.CandidatePOS.Any(p => p is PartOfSpeech.Noun or PartOfSpeech.CommonNoun
                                                   or PartOfSpeech.NaAdjective or PartOfSpeech.Pronoun
                                                   or PartOfSpeech.Name or PartOfSpeech.NominalAdjective),

                ScoringCondition.CandidateIsNaAdj =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.NaAdjective),

                ScoringCondition.CandidateIsAdverb =>
                    ctx.CandidatePOS.Any(p => p is PartOfSpeech.Adverb or PartOfSpeech.AdverbTo),

                ScoringCondition.CandidateIsAuxiliary =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.Auxiliary),

                ScoringCondition.CandidateIsParticle =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.Particle),

                ScoringCondition.CandidateIsSingleKanaNonParticle =>
                    ctx.CandidateText.Length <= 1 && !ctx.CandidatePOS.Contains(PartOfSpeech.Particle),

                ScoringCondition.NextIsCommonParticle =>
                    ctx.NextPOS?.Contains(PartOfSpeech.Particle) == true
                    && ctx.NextText != null && TransitionRuleSets.CommonParticles.Contains(ctx.NextText),

                ScoringCondition.NextIsCopula =>
                    ctx.NextText != null && TransitionRuleSets.CopulaForms.Contains(ctx.NextText),

                ScoringCondition.NextIsNaConnector =>
                    ctx.NextText is "な" or "に",

                ScoringCondition.NextIsVerbOrIAdj =>
                    ctx.NextPOS != null
                    && (ctx.NextPOS.Contains(PartOfSpeech.Verb) || ctx.NextPOS.Contains(PartOfSpeech.IAdjective)),

                ScoringCondition.PrevIsVerbOrIAdj =>
                    ctx.PrevPOS != null
                    && (ctx.PrevPOS.Contains(PartOfSpeech.Verb) || ctx.PrevPOS.Contains(PartOfSpeech.IAdjective)),

                ScoringCondition.PrevIsParticle =>
                    ctx.PrevPOS?.Contains(PartOfSpeech.Particle) == true,

                ScoringCondition.NextIsParticle =>
                    ctx.NextPOS?.Contains(PartOfSpeech.Particle) == true,

                ScoringCondition.PrevIsSingleKanaNonParticle =>
                    ctx.PrevText is { Length: 1 } && ctx.PrevPOS?.Contains(PartOfSpeech.Particle) != true,

                ScoringCondition.NextIsSingleKanaNonParticle =>
                    ctx.NextText is { Length: 1 } && ctx.NextPOS?.Contains(PartOfSpeech.Particle) != true,

                ScoringCondition.CandidateIsPredicateHost =>
                    ctx.CandidatePOS.Any(p => p is PartOfSpeech.Verb
                                                 or PartOfSpeech.IAdjective
                                                 or PartOfSpeech.Auxiliary),

                ScoringCondition.CandidateIsNoParticle =>
                    ctx.CandidateText == "の" && ctx.CandidatePOS.Contains(PartOfSpeech.Particle),

                ScoringCondition.NextIsExplanatoryN =>
                    ctx.NextText != null
                    && TransitionRuleSets.ExplanatoryNForms.Contains(ctx.NextText),

                ScoringCondition.PrevIsVerbAuxOrIAdj =>
                    ctx.PrevPOS != null
                    && (ctx.PrevPOS.Contains(PartOfSpeech.Verb)
                        || ctx.PrevPOS.Contains(PartOfSpeech.Auxiliary)
                        || ctx.PrevPOS.Contains(PartOfSpeech.IAdjective)),

                ScoringCondition.CandidateIsCounter =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.Counter),

                ScoringCondition.PrevIsNumeral =>
                    ctx.PrevPOS?.Contains(PartOfSpeech.Numeral) == true,

                ScoringCondition.PrevIsNotNumericLike =>
                    ctx.PrevPOS == null
                    || !ctx.PrevPOS.Any(p => p is PartOfSpeech.Numeral or PartOfSpeech.Noun
                                                or PartOfSpeech.CommonNoun or PartOfSpeech.Pronoun
                                                or PartOfSpeech.Name),

                ScoringCondition.CandidateIsSingleKanji =>
                    IsSingleKanji(ctx.CandidateText),

                ScoringCondition.PrevIsSingleKanji =>
                    IsSingleKanji(ctx.PrevText),

                ScoringCondition.NextIsSingleKanji =>
                    IsSingleKanji(ctx.NextText),

                ScoringCondition.NextIsConditionalParticle =>
                    ctx.NextPOS?.Contains(PartOfSpeech.Particle) == true
                    && ctx.NextText != null && TransitionRuleSets.ConditionalParticles.Contains(ctx.NextText),

                ScoringCondition.CandidateIsAdvTo =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.AdverbTo),

                ScoringCondition.NextIsToParticle =>
                    ctx.NextText == "と" && ctx.NextPOS?.Contains(PartOfSpeech.Particle) == true,

                ScoringCondition.CandidateIsVerb =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.Verb),

                ScoringCondition.NextIsTeFormAux =>
                    ctx.NextText != null && TransitionRuleSets.TeFormAuxiliaries.Contains(ctx.NextText),

                ScoringCondition.PrevIsNoParticle =>
                    ctx.PrevText == "の" && ctx.PrevPOS?.Contains(PartOfSpeech.Particle) == true,

                ScoringCondition.NextIsNotNaAdjConnector =>
                    ctx.NextText != null
                    && ctx.NextText is not ("な" or "に" or "で" or "の")
                    && !TransitionRuleSets.CopulaForms.Contains(ctx.NextText)
                    && ctx.NextPOS?.Contains(PartOfSpeech.Particle) != true
                    && ctx.NextPOS?.Any(p => p is PartOfSpeech.Suffix or PartOfSpeech.NounSuffix) != true,

                ScoringCondition.NextIsBaParticle =>
                    ctx.NextText == "ば" && ctx.NextPOS?.Contains(PartOfSpeech.Particle) == true,

                ScoringCondition.CandidateIsPrenounAdjectival =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.PrenounAdjectival),

                ScoringCondition.NextIsNounLike =>
                    ctx.NextPOS?.Any(p => p is PartOfSpeech.Noun or PartOfSpeech.CommonNoun
                                            or PartOfSpeech.NaAdjective or PartOfSpeech.Pronoun
                                            or PartOfSpeech.Name or PartOfSpeech.NominalAdjective) == true,

                ScoringCondition.NextIsNotNounLike =>
                    ctx.NextPOS == null
                    || !ctx.NextPOS.Any(p => p is PartOfSpeech.Noun or PartOfSpeech.CommonNoun
                                               or PartOfSpeech.NaAdjective or PartOfSpeech.Pronoun
                                               or PartOfSpeech.Name or PartOfSpeech.NominalAdjective),

                ScoringCondition.CandidateIsConjunction =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.Conjunction),

                ScoringCondition.IsSentenceInitial =>
                    ctx.PrevPOS == null && ctx.PrevText == null,

                ScoringCondition.CandidateIsInterjection =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.Interjection)
                    && TransitionRuleSets.Interjections.Contains(ctx.CandidateText),

                ScoringCondition.CandidateIsSuruNoun =>
                    ctx.CandidateIsSuruNounVal,

                ScoringCondition.NextIsSuru =>
                    ctx.NextText != null && TransitionRuleSets.SuruForms.Contains(ctx.NextText),

                ScoringCondition.PrevIsCaseParticle =>
                    ctx.PrevPOS?.Contains(PartOfSpeech.Particle) == true
                    && ctx.PrevText != null && TransitionRuleSets.CaseMarkingParticles.Contains(ctx.PrevText),

                ScoringCondition.CandidateIsName =>
                    ctx.CandidatePOS.Contains(PartOfSpeech.Name),

                ScoringCondition.NextIsHonorific =>
                    ctx.NextText != null && TransitionRuleSets.HonorificSuffixes.Contains(ctx.NextText),

                ScoringCondition.IsSentenceFinal =>
                    ctx.NextPOS == null && ctx.NextText == null,

                ScoringCondition.CandidateIsNounSuffix =>
                    ctx.CandidatePOS.Any(p => p is PartOfSpeech.Suffix or PartOfSpeech.NounSuffix)
                    && TransitionRuleSets.NounSuffixes.Contains(ctx.CandidateText),

                ScoringCondition.PrevIsNounLike =>
                    ctx.PrevPOS?.Any(p => p is PartOfSpeech.Noun or PartOfSpeech.CommonNoun
                                            or PartOfSpeech.NaAdjective or PartOfSpeech.Pronoun
                                            or PartOfSpeech.Name or PartOfSpeech.NominalAdjective) == true,

                ScoringCondition.CandidateIsNotNounLike =>
                    !ctx.CandidatePOS.Any(p => p is PartOfSpeech.Noun or PartOfSpeech.CommonNoun
                                                   or PartOfSpeech.NaAdjective or PartOfSpeech.Pronoun
                                                   or PartOfSpeech.Name or PartOfSpeech.NominalAdjective),

                _ => false
            };
            if (!ok) return false;
        }

        return true;
    }
}
