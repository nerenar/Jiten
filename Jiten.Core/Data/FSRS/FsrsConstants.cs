namespace Jiten.Core.Data.FSRS;

/// <summary>
/// Contains constants and default parameters for the FSRS algorithm
/// </summary>
public static class FsrsConstants
{
    /// <summary>
    /// Default target retention rate
    /// </summary>
    public const double DefaultDesiredRetention = 0.9;

    /// <summary>
    /// Minimum allowed stability value to prevent division by zero
    /// </summary>
    public const double StabilityMin = 0.001;

    public const double StabilityMax = 36500.0;

    /// <summary>
    /// Default FSRS algorithm parameters optimized for general use
    /// </summary>
    public static readonly double[] DefaultParameters =
    [
        0.212, 1.2931, 2.3065, 8.2956, 6.4133, 0.8334,
        3.0194, 0.001, 1.8722, 0.1666, 0.796,
        1.4835, 0.0614, 0.2629, 1.6483, 0.6014,
        1.8729, 0.5425, 0.0912, 0.0658, 0.1542,
    ];

    /// <summary>
    /// Fuzzing ranges for interval randomization
    /// </summary>
    public static readonly FsrsFuzzRange[] FuzzRanges =
    [
        new(2.5, 7.0, 0.15),
        new(7.0, 20.0, 0.1),
        new(20.0, double.PositiveInfinity, 0.05)
    ];
}
