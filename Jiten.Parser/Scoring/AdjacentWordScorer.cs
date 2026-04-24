using Jiten.Core.Data;
using Jiten.Parser.Grammar;

namespace Jiten.Parser.Scoring;

internal static class AdjacentWordScorer
{
    internal readonly record struct AdjacentContext(
        uint PrevMask,
        bool HasPrev,
        string? PrevText,
        uint NextMask,
        bool HasNext,
        string? NextText)
    {
        public static AdjacentContext Create(
            List<PartOfSpeech>? prevPOS, string? prevText,
            List<PartOfSpeech>? nextPOS, string? nextText) => new(
            prevPOS != null ? PosMask.FromList(prevPOS) : 0,
            prevPOS != null,
            prevText,
            nextPOS != null ? PosMask.FromList(nextPOS) : 0,
            nextPOS != null,
            nextText);
    }

    internal static int CalculateContextBonusOnly(
        FormCandidate candidate,
        AdjacentContext context)
    {
        var window = new ScoringWindow(
            candidate,
            context.PrevMask, context.HasPrev, context.PrevText,
            context.NextMask, context.HasNext, context.NextText);

        return TransitionRuleEngine.EvaluateSoftRulesBonus(window);
    }

    internal static (int bonus, List<string> rulesMatched) CalculateContextBonus(
        FormCandidate candidate,
        AdjacentContext context)
    {
        var window = new ScoringWindow(
            candidate,
            context.PrevMask, context.HasPrev, context.PrevText,
            context.NextMask, context.HasNext, context.NextText);

        return TransitionRuleEngine.EvaluateSoftRules(window);
    }
}
