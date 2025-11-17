namespace Jiten.Core.Data.FSRS;

public static class FsrsHelper
{
    private static readonly Random Random = new();

    /// <summary>
    /// Calculates initial difficulty for a new card
    /// </summary>
    public static double CalculateInitialDifficulty(FsrsRating rating, double[] parameters)
    {
        var initialDifficulty = parameters[4] - Math.Exp(parameters[5] * ((int)rating - 1)) + 1;

        return ClampDifficulty(initialDifficulty);
    }

    /// <summary>
    /// Updates difficulty based on review performance
    /// </summary>
    public static double CalculateNextDifficulty(double difficulty, FsrsRating rating, double[] parameters)
    {
        var arg1 = CalculateInitialDifficulty(FsrsRating.Easy, parameters);
        var deltaDifficulty = -(parameters[6] * ((int)rating - 3));
        var arg2 = difficulty + LinearDamping(deltaDifficulty, difficulty);
        var nextDifficulty = MeanReversion(arg1, arg2, parameters[7]);

        return ClampDifficulty(nextDifficulty);
    }

    private static double LinearDamping(double deltaDifficulty, double difficulty)
    {
        return (10.0 - difficulty) * deltaDifficulty / 9.0;
    }

    private static double MeanReversion(double arg1, double arg2, double parameter)
    {
        return parameter * arg1 + (1 - parameter) * arg2;
    }

    private static double ClampDifficulty(double difficulty)
    {
        return Math.Clamp(difficulty, 1.0, 10.0);
    }

    /// <summary>
    /// Applies fuzzing to a review interval
    /// </summary>
    /// <param name="interval">Base interval to fuzz</param>
    /// <param name="maximumInterval">Maximum allowed interval</param>
    /// <returns>Fuzzed interval</returns>
    public static TimeSpan ApplyFuzzing(TimeSpan interval, int maximumInterval)
    {
        var intervalDays = interval.Days;

        if (intervalDays < 2.5)
            return interval;

        var (minInterval, maxInterval) = GetFuzzRange(intervalDays, maximumInterval);
        var fuzzedDays = Random.NextDouble() * (maxInterval - minInterval + 1) + minInterval;
        var clampedDays = Math.Min(Math.Round(fuzzedDays), maximumInterval);

        return TimeSpan.FromDays(clampedDays);
    }

    private static (int Min, int Max) GetFuzzRange(int intervalDays, int maximumInterval)
    {
        var delta = 1.0;

        foreach (var fuzzRange in FsrsConstants.FuzzRanges)
        {
            delta += fuzzRange.Factor * Math.Max(
                                                 Math.Min(intervalDays, fuzzRange.End) - fuzzRange.Start, 0.0);
        }

        var minInterval = Math.Max(2, (int)Math.Round(intervalDays - delta));
        var maxInterval = Math.Min((int)Math.Round(intervalDays + delta), maximumInterval);
        minInterval = Math.Min(minInterval, maxInterval);

        return (minInterval, maxInterval);
    }

    /// <summary>
    /// Calculates the next review interval in days
    /// </summary>
    /// <param name="stability">Current card stability</param>
    /// <param name="desiredRetention">Target retention rate (0-1)</param>
    /// <param name="parameters">FSRS algorithm parameters</param>
    /// <param name="maximumInterval">Maximum allowed interval in days</param>
    /// <returns>Next review interval in days</returns>
    public static int CalculateNextInterval(double stability, double desiredRetention, double[] parameters, int maximumInterval)
    {
        var decay = -parameters[20];
        var factor = Math.Pow(0.9, 1.0 / decay) - 1;

        var nextInterval = (stability / factor) * (Math.Pow(desiredRetention, 1.0 / decay) - 1);
        var roundedInterval = (int)Math.Round(nextInterval);

        return Math.Clamp(roundedInterval, 1, maximumInterval);
    }

    /// <summary>
    /// Calculates the current retrievability (recall probability) of a card
    /// </summary>
    /// <param name="card">The card to calculate retrievability for</param>
    /// <param name="currentDateTime">Current time (defaults to now)</param>
    /// <param name="parameters">FSRS parameters (defaults to standard parameters)</param>
    /// <returns>Retrievability value between 0 and 1</returns>
    public static double CalculateRetrievability(FsrsCard card, DateTime? currentDateTime = null, double[]? parameters = null)
    {
        if (card.LastReview == null)
            return 0;

        currentDateTime ??= DateTime.UtcNow;
        parameters ??= FsrsConstants.DefaultParameters;

        var decay = -parameters[20];
        var factor = Math.Pow(0.9, 1.0 / decay) - 1;
        var elapsedDays = Math.Max(0, (currentDateTime.Value - card.LastReview.Value).TotalDays);

        var stability = card.Stability ?? 1.0d;
        
        return Math.Pow(1 + factor * elapsedDays / stability, decay);
    }

    /// <summary>
    /// Calculates initial stability for a new card
    /// </summary>
    public static double CalculateInitialStability(FsrsRating rating, double[] parameters)
    {
        var initialStability = parameters[(int)rating - 1];

        return ClampStability(initialStability);
    }

    /// <summary>
    /// Calculates stability for short-term reviews (within same day)
    /// </summary>
    public static double CalculateShortTermStability(double stability, FsrsRating rating, double[] parameters)
    {
        var shortTermStabilityIncrease = Math.Exp(parameters[17] * ((int)rating - 3 + parameters[18]))
                                         * Math.Pow(stability, -parameters[19]);

        if (rating is FsrsRating.Good or FsrsRating.Easy)
        {
            shortTermStabilityIncrease = Math.Max(shortTermStabilityIncrease, 1.0);
        }

        var shortTermStability = stability * shortTermStabilityIncrease;

        return ClampStability(shortTermStability);
    }

    /// <summary>
    /// Calculates stability for long-term reviews
    /// </summary>
    public static double CalculateNextStability(double difficulty, double stability, double retrievability, FsrsRating rating,
                                                double[] parameters)
    {
        var nextStability = rating == FsrsRating.Again
            ? FsrsHelper.CalculateNextForgetStability(difficulty, stability, retrievability, parameters)
            : FsrsHelper.CalculateNextRecallStability(difficulty, stability, retrievability, rating, parameters);

        return ClampStability(nextStability);
    }

    private static double CalculateNextForgetStability(double difficulty, double stability, double retrievability, double[] parameters)
    {
        var longTermParams = parameters[11]
                             * Math.Pow(difficulty, -parameters[12])
                             * (Math.Pow(stability + 1, parameters[13]) - 1)
                             * Math.Exp((1 - retrievability) * parameters[14]);

        var shortTermParams = stability / Math.Exp(parameters[17] * parameters[18]);

        return Math.Min(longTermParams, shortTermParams);
    }

    private static double CalculateNextRecallStability(double difficulty, double stability, double retrievability, FsrsRating rating,
                                                double[] parameters)
    {
        var hardPenalty = rating == FsrsRating.Hard ? parameters[15] : 1;
        var easyBonus = rating == FsrsRating.Easy ? parameters[16] : 1;

        return stability * (1 + Math.Exp(parameters[8]) * (11 - difficulty)
                                                        * Math.Pow(stability, -parameters[9])
                                                        * (Math.Exp((1 - retrievability) * parameters[10]) - 1)
                                                        * hardPenalty * easyBonus);
    }

    private static double ClampStability(double stability)
    {
        return Math.Max(stability, FsrsConstants.StabilityMin);
    }
}