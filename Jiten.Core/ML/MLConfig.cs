namespace Jiten.Cli.ML;

public static class MLConfig
{
    public const string DifficultyCsvFile = "difficulty.csv";
    public const string OutputCsvPath = "linguistic_features_csharp.csv";

    public const int SaveInterval = 100;
    public const int NumThreads = 8;
    public const int ChunkProcessingTimeoutSeconds = 60;

    public const int LowFreqRankThreshold = 30000;
    public const double LowFreqObserverThreshold = 9e-6;

    // Rank Score Parameters
    public const int RankBase = 5000;
    public const int RankExpBase = 10000;
    public const int RankSoftcapStart = 90000;
    public const int RankSoftcapStep = 10000;
    public const int RankHardcap = 115000;
    public static readonly double ScoreHardcapValue;

    public static readonly Dictionary<string, List<string>> ConjugationCategories = new Dictionary<string, List<string>>
                                                                                    {
                                                                                        {
                                                                                            "negative",
                                                                                            new List<string>
                                                                                            {
                                                                                                "negative", "slurred negative",
                                                                                                "adverbial negative", "without doing so",
                                                                                                "archaic negative", "formal negative",
                                                                                                "formal negative past", "negative polite",
                                                                                                "past negative polite"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "polite",
                                                                                            new List<string>
                                                                                            {
                                                                                                "polite", "negative polite", "past polite",
                                                                                                "te polite", "past negative polite",
                                                                                                "polite volitional", "formal conditional",
                                                                                                "polite request", "kind request",
                                                                                                "casual kind request"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "conditional",
                                                                                            new List<string>
                                                                                            {
                                                                                                "conditional", "formal conditional",
                                                                                                "provisional conditional"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "passive_causative",
                                                                                            new List<string>
                                                                                            {
                                                                                                "passive", "passive/potential",
                                                                                                "short causative", "causative"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "potential",
                                                                                            new List<string>
                                                                                            {
                                                                                                "potential", "passive/potential"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "volitional",
                                                                                            new List<string>
                                                                                            {
                                                                                                "volitional", "polite volitional"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "imperative",
                                                                                            new List<string>
                                                                                            {
                                                                                                "imperative", "kind request",
                                                                                                "casual kind request"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "te_form",
                                                                                            new List<string>
                                                                                            {
                                                                                                "(te form)", "teiru", "teru (teiru)",
                                                                                                "teoru", "toru (teoru)", "tearu", "teiku",
                                                                                                "teku (teiku)", "tekuru"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "past",
                                                                                            new List<string>
                                                                                            {
                                                                                                "past", "formal negative past",
                                                                                                "past polite", "past negative polite",
                                                                                                "tari"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "stem",
                                                                                            new List<string>
                                                                                            {
                                                                                                "('a' stem)", "(adverbial stem)",
                                                                                                "(ka stem)", "(ke stem)", "(stem)",
                                                                                                "(izenkei)", "(mizenkei)",
                                                                                                "(unstressed infinitive)", "(infinitive)"
                                                                                            }
                                                                                        },
                                                                                        {
                                                                                            "other",
                                                                                            new List<string>
                                                                                            {
                                                                                                "excess", "seemingness", "garu",
                                                                                                "noun form", "finish/completely/end up",
                                                                                                "do for someone", "for now",
                                                                                                "toku (for now)", "topic/condition",
                                                                                                "while", "want", "too much", "contracted",
                                                                                                "slurred", "(unstressed infinitive)",
                                                                                                "(infinitive)", ""
                                                                                            }
                                                                                        }
                                                                                    };

    public static readonly Dictionary<string, string> ConjugationDetailToCategory;

    static MLConfig()
    {
        // Calculate ScoreHardcapValue
        double scoreAtSoftcapStart = RankBase;
        int currentRankThreshold = RankBase;
        double multiplier = 1;
        while (currentRankThreshold < RankSoftcapStart)
        {
            multiplier *= 2;
            int nextThreshold = currentRankThreshold + RankExpBase;
            if (nextThreshold <= RankSoftcapStart)
            {
                scoreAtSoftcapStart += RankExpBase * multiplier;
            }
            else
            {
                scoreAtSoftcapStart += (RankSoftcapStart - currentRankThreshold) * multiplier;
            }

            currentRankThreshold = nextThreshold;
        }

        double scoreAtHardcap = scoreAtSoftcapStart;
        currentRankThreshold = RankSoftcapStart;
        double multiplierAtSoftcapStart = RankSoftcapStart > RankBase
            ? Math.Pow(2, Math.Ceiling((double)(RankSoftcapStart - RankBase) / RankExpBase))
            : 1;

        while (currentRankThreshold < RankHardcap)
        {
            multiplier = multiplierAtSoftcapStart;
            int nextThreshold = currentRankThreshold + RankSoftcapStep;
            if (nextThreshold <= RankHardcap)
            {
                scoreAtHardcap += RankSoftcapStep * multiplier;
            }
            else
            {
                scoreAtHardcap += (RankHardcap - currentRankThreshold) * multiplier;
            }

            currentRankThreshold = nextThreshold;
        }

        ScoreHardcapValue = scoreAtHardcap;

        // Initialize ConjugationDetailToCategory
        ConjugationDetailToCategory = new Dictionary<string, string>();
        foreach (var kvp in ConjugationCategories)
        {
            foreach (var detail in kvp.Value)
            {
                ConjugationDetailToCategory[detail] = kvp.Key;
            }
        }
    }
}