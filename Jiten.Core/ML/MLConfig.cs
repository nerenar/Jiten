using Jiten.Core.Data;

namespace Jiten.Cli.ML;

public static class MLConfig
{
    public const string DifficultyCsvFile = "difficulty.csv";
    public const string OutputCsvPath = "linguistic_features_csharp.csv";

    public const int SaveInterval = 25;
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

    public static readonly Dictionary<string, List<string>> ConjugationCategories = new()
                                                                                    {
                                                                                        {
                                                                                            "negative", [
                                                                                                "negative", "slurred negative",
                                                                                                "adverbial negative", "without doing so",
                                                                                                "archaic negative", "formal negative",
                                                                                                "formal negative past", "negative polite",
                                                                                                "past negative polite"
                                                                                            ]
                                                                                        },
                                                                                        {
                                                                                            "polite", [
                                                                                                "polite", "negative polite", "past polite",
                                                                                                "te polite", "past negative polite",
                                                                                                "polite volitional", "formal conditional",
                                                                                                "polite request", "kind request",
                                                                                                "casual kind request"
                                                                                            ]
                                                                                        },
                                                                                        {
                                                                                            "conditional", [
                                                                                                "conditional", "formal conditional",
                                                                                                "provisional conditional"
                                                                                            ]
                                                                                        },
                                                                                        {
                                                                                            "passive_causative", [
                                                                                                "passive", "passive/potential",
                                                                                                "short causative", "causative"
                                                                                            ]
                                                                                        },
                                                                                        { "potential", ["potential", "passive/potential"] },
                                                                                        {
                                                                                            "volitional",
                                                                                            ["volitional", "polite volitional"]
                                                                                        },
                                                                                        {
                                                                                            "imperative", [
                                                                                                "imperative", "kind request",
                                                                                                "casual kind request"
                                                                                            ]
                                                                                        },
                                                                                        {
                                                                                            "te_form", [
                                                                                                "(te form)", "teiru", "teru (teiru)",
                                                                                                "teoru", "toru (teoru)", "tearu", "teiku",
                                                                                                "teku (teiku)", "tekuru"
                                                                                            ]
                                                                                        },
                                                                                        {
                                                                                            "past", [
                                                                                                "past", "formal negative past",
                                                                                                "past polite", "past negative polite",
                                                                                                "tari"
                                                                                            ]
                                                                                        },
                                                                                        {
                                                                                            "stem", [
                                                                                                "('a' stem)", "(adverbial stem)",
                                                                                                "(ka stem)", "(ke stem)", "(stem)",
                                                                                                "(izenkei)", "(mizenkei)",
                                                                                                "(unstressed infinitive)", "(infinitive)"
                                                                                            ]
                                                                                        },
                                                                                        { "garu", ["garu"] },
                                                                                        { "seemingness", ["seemingness"] },
                                                                                        { "shimau", ["finish/completely/end up"] },
                                                                                        { "contracted", ["contracted", "slurred"] },
                                                                                        {
                                                                                            "other", [
                                                                                                "excess",
                                                                                                "noun form",
                                                                                                "do for someone", "for now",
                                                                                                "toku (for now)", "topic/condition",
                                                                                                "while", "want", "too much",
                                                                                                "(unstressed infinitive)",
                                                                                                "(infinitive)", ""
                                                                                            ]
                                                                                        }
                                                                                    };

    public static readonly Dictionary<string, List<PartOfSpeech>> PosCategories = new()
                                                                                  {
                                                                                      {
                                                                                          "noun",
                                                                                          [PartOfSpeech.Noun, PartOfSpeech.CommonNoun]
                                                                                      },
                                                                                      { "verb", [PartOfSpeech.Verb] },
                                                                                      {
                                                                                          "adj", [
                                                                                              PartOfSpeech.IAdjective,
                                                                                              PartOfSpeech.NaAdjective,
                                                                                              PartOfSpeech.Adnominal,
                                                                                              PartOfSpeech.NominalAdjective,
                                                                                              PartOfSpeech.PrenounAdjectival
                                                                                          ]
                                                                                      },
                                                                                      { "adv", [PartOfSpeech.Adverb] },
                                                                                      // { "part", [PartOfSpeech.Particle] },
                                                                                      { "conjunc", [PartOfSpeech.Conjunction] },
                                                                                      { "aux", [PartOfSpeech.Auxiliary] },
                                                                                      { "inter", [PartOfSpeech.Interjection] },
                                                                                      { "fix", [PartOfSpeech.Prefix, PartOfSpeech.Suffix] },
                                                                                      // { "filler", [PartOfSpeech.Filler] },
                                                                                      // { "name", [PartOfSpeech.Name] },
                                                                                      { "pn", [PartOfSpeech.Pronoun] },
                                                                                      { "exp", [PartOfSpeech.Expression] },
                                                                                      { "other", [PartOfSpeech.Unknown] }
                                                                                  };


    public static readonly Dictionary<string, string> ConjugationDetailToCategory;
    public static readonly Dictionary<PartOfSpeech, string> PosDetailToCategory;

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

        ConjugationDetailToCategory = new Dictionary<string, string>();
        foreach (var kvp in ConjugationCategories)
        {
            foreach (var detail in kvp.Value)
            {
                ConjugationDetailToCategory[detail] = kvp.Key;
            }
        }

        PosDetailToCategory = new Dictionary<PartOfSpeech, string>();
        foreach (var kvp in PosCategories)
        {
            foreach (var detail in kvp.Value)
            {
                PosDetailToCategory[detail] = kvp.Key;
            }
        }
    }
}