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
        if (allCandidates.Count == 0)
            return null;

        FormCandidate? best = null;
        int bestScore = int.MinValue;

        foreach (var candidate in allCandidates)
        {
            var trace = FormCandidateScorer.Score(candidate, context, archaicPosTypes);
            candidate.SetScoreTrace(trace);

            int score = trace.TotalScore;
            if (score > bestScore ||
                (score == bestScore && best != null && candidate.Word.WordId < best.Word.WordId) ||
                (score == bestScore && best != null && candidate.Word.WordId == best.Word.WordId
                                   && HasPreferredConjugation(candidate, best)))
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (diagnostics == null || best == null)
            return best;

        var topCandidates = allCandidates
                            .OrderByDescending(c => c.TotalScore)
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
                                    Candidates = topCandidates
                                });

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
