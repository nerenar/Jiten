namespace Jiten.Parser.Scoring;

internal static class ScoringPolicy
{
    public const int PosIncompatiblePenalty = 15;
    public const int LowConfidenceThreshold = 15;
    public const int HighConfidenceThreshold = 40;

    public static int EffectiveScore(FormCandidate c) =>
        c.TotalScore - (c.IsPosIncompatibleDirectSurface ? PosIncompatiblePenalty : 0);
}
