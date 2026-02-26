using Jiten.Parser.Diagnostics;

namespace Jiten.Parser.Scoring;

internal static class FormCandidateSelector
{
    public static FormCandidate? PickBestCandidate(
        List<FormCandidate> allCandidates,
        FormScoringContext context,
        IReadOnlySet<string> archaicPosTypes,
        ParserDiagnostics? diagnostics = null)
    {
        return PickTopCandidates(allCandidates, context, archaicPosTypes, diagnostics).Best;
    }

    public static CandidateSelectionResult PickTopCandidates(
        List<FormCandidate> allCandidates,
        FormScoringContext context,
        IReadOnlySet<string> archaicPosTypes,
        ParserDiagnostics? diagnostics = null,
        int topN = 5)
    {
        if (allCandidates.Count == 0)
            return new CandidateSelectionResult(null, [], null);

        // POS-incompatible direct-surface candidates (e.g. noun 1197950 "artistry" competing with
        // adj-na 2653620 "serious" when Sudachi tags the token as NaAdjective) get a -15 penalty
        // so POS-compatible matches win the close races they should win.
        static int EffectiveScore(FormCandidate c) => ScoringPolicy.EffectiveScore(c);

        FormCandidate? best = null;
        int bestScore = int.MinValue;

        foreach (var candidate in allCandidates)
        {
            var trace = FormCandidateScorer.Score(candidate, context, archaicPosTypes);
            candidate.SetScoreTrace(trace);

            int score = EffectiveScore(candidate);
            if (score > bestScore ||
                (score == bestScore && best != null && candidate.Word.WordId < best.Word.WordId) ||
                (score == bestScore && best != null && candidate.Word.WordId == best.Word.WordId
                                   && HasPreferredConjugation(candidate, best)))
            {
                bestScore = score;
                best = candidate;
            }
        }

        var sorted = allCandidates.OrderByDescending(EffectiveScore).ToList();
        var top = sorted.Take(topN).ToList();
        // Filter out POS-incompatible runners-up so their -15 penalty doesn't produce a false low margin.
        // Fallback: if ALL candidates are POS-incompatible, use the full pool (WordId != guard prevents self-comparison).
        var legitimateCandidates = sorted.Where(c => !c.IsPosIncompatibleDirectSurface).ToList();
        var alternatePool = legitimateCandidates.Count > 0 ? legitimateCandidates : sorted;
        int? margin = null;
        if (best != null)
        {
            var bestAlternate = alternatePool.FirstOrDefault(c => c.Word.WordId != best.Word.WordId);
            if (bestAlternate != null)
                margin = ScoringPolicy.EffectiveScore(best) - ScoringPolicy.EffectiveScore(bestAlternate);
        }

        if (diagnostics != null && best != null)
        {
            var topCandidates = sorted
                                .Take(10)
                                .Select(c => new FormCandidateDiagnostic
                                             {
                                                 WordId = c.Word.WordId,
                                                 FormText = c.Form.Text,
                                                 ReadingIndex = c.ReadingIndex,
                                                 IsSelected = ReferenceEquals(c, best),
                                                 TotalScore = c.TotalScore,
                                                 WordScore = c.WordScore,
                                                 EntryPriorityScore = c.EntryPriorityScore,
                                                 FormPriorityScore = c.FormPriorityScore,
                                                 FormFlagScore = c.FormFlagScore,
                                                 SurfaceMatchScore = c.SurfaceMatchScore,
                                                 ScriptScore = c.ScriptScore,
                                                 ReadingMatchScore = c.ReadingMatchScore
                                             })
                                .ToList();

            diagnostics.Results.Add(new WordResult
                                    {
                                        Text = context.Surface,
                                        DictionaryForm = context.DictionaryForm,
                                        Reading = context.SudachiReading,
                                        WordId = best.Word.WordId,
                                        ReadingIndex = best.ReadingIndex,
                                        Candidates = topCandidates,
                                        MarginToSecond = margin
                                    });
        }

        return new CandidateSelectionResult(best, top, margin);
    }

    /// Selects the best candidate using pre-scored candidates + a per-candidate bonus function.
    /// Candidates must already have scores set via FormCandidateScorer.Score before calling this.
    public static FormCandidate? PickTopCandidatesWithBonus(
        List<FormCandidate> allCandidates,
        Func<FormCandidate, int> bonusFunc)
    {
        if (allCandidates.Count == 0) return null;

        FormCandidate? best = null;
        int bestAdjusted = int.MinValue;

        foreach (var candidate in allCandidates)
        {
            int adjusted = ScoringPolicy.EffectiveScore(candidate) + bonusFunc(candidate);
            if (adjusted > bestAdjusted ||
                (adjusted == bestAdjusted && best != null && candidate.Word.WordId < best.Word.WordId) ||
                (adjusted == bestAdjusted && best != null && candidate.Word.WordId == best.Word.WordId
                                           && HasPreferredConjugation(candidate, best)))
            {
                bestAdjusted = adjusted;
                best = candidate;
            }
        }

        return best;
    }

    private static bool HasPreferredConjugation(FormCandidate candidate, FormCandidate current)
    {
        var candidateProcess = candidate.DeconjForm?.Process;
        var currentProcess = current.DeconjForm?.Process;

        if (candidateProcess is not { Count: > 0 } || currentProcess is not { Count: > 0 })
            return false;

        bool candidateIsInfinitive = candidateProcess[^1] is "(infinitive)" or "(unstressed infinitive)";
        bool currentIsInfinitive = currentProcess[^1] is "(infinitive)" or "(unstressed infinitive)";

        return candidateIsInfinitive && !currentIsInfinitive;
    }
}
