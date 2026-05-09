namespace Jiten.Parser.Scoring;

internal static class ScoringPolicy
{
    public const int PosIncompatiblePenalty = 15;
    public const int LowConfidenceThreshold = 15;
    public const int HighConfidenceThreshold = 40;

    public static bool IsHighConfidence(int? margin) => margin.HasValue && margin.Value >= HighConfidenceThreshold;
    public static bool IsLowConfidence(int? margin) => margin.HasValue && margin.Value < LowConfidenceThreshold;

    public static int EffectiveScore(FormCandidate c) =>
        c.TotalScore - (c.IsPosIncompatibleDirectSurface ? PosIncompatiblePenalty : 0);
}
