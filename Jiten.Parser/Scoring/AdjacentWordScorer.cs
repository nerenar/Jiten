using Jiten.Core.Data;
using Jiten.Parser.Grammar;

namespace Jiten.Parser.Scoring;

internal static class AdjacentWordScorer
{
    internal readonly record struct AdjacentContext(
        List<PartOfSpeech>? PrevResolvedPOS,
        List<PartOfSpeech>? NextResolvedPOS,
        string? PrevText,
        string? NextText);

    internal static (int bonus, List<string> rulesMatched) CalculateContextBonus(
        FormCandidate candidate,
        AdjacentContext context)
    {
        var window = new ScoringWindow(
            candidate,
            context.PrevResolvedPOS,
            context.NextResolvedPOS,
            context.PrevText,
            context.NextText);

        return TransitionRuleEngine.EvaluateSoftRules(window);
    }
}
