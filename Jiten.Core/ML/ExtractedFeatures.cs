namespace Jiten.Cli.ML;

public class ExtractedFeatures
{
    public required string Filename { get; set; }
    public double DifficultyRating { get; set; }

    // Parser features
    public double CharacterCount { get; set; }
    public double WordCount { get; set; }
    public double UniqueWordCount { get; set; }
    public double UniqueWordOnceCount { get; set; }
    public double UniqueKanjiCount { get; set; }
    public double UniqueKanjiOnceCount { get; set; }
    public double SentenceCount { get; set; }
    public double Ttr { get; set; }
    public double AverageSentenceLength { get; set; }
    public double DialoguePercentage { get; set; }

    // Calculated counts Counts
    public int TotalCount { get; set; }
    public int KanjiCount { get; set; }
    public int HiraganaCount { get; set; }
    public int KatakanaCount { get; set; }
    public int OtherCount { get; set; }
    public double KanjiRatio { get; set; }
    public double HiraganaRatio { get; set; }
    public double KatakanaRatio { get; set; }
    public double OtherRatio { get; set; }
    public double KanjiToKanaRatio { get; set; }


    // Frequency Stats
    public double AvgLogFreqRank { get; set; }
    public double MedianLogFreqRank { get; set; }
    public double StdLogFreqRank { get; set; }
    public double MinFreqRank { get; set; }
    public double MaxFreqRank { get; set; }
    public double AvgLogObsFreq { get; set; }
    public double MedianLogObsFreq { get; set; }
    public double StdLogObsFreq { get; set; }
    public double MinObsFreq { get; set; }
    public double MaxObsFreq { get; set; }
    public double LowFreqRankPerc { get; set; }
    public double LowFreqObsPerc { get; set; }
    public double AvgReadingFreqRank { get; set; }
    public double MedianReadingFreqRank { get; set; }
    public double AvgReadingObsFreq { get; set; }
    public double MedianReadingObsFreq { get; set; }
    public double AvgReadingFreqPerc { get; set; }
    public double MedianReadingFreqPerc { get; set; }
    public double AvgCustomScorePerWord { get; set; }
    public double MedianCustomWordScore { get; set; }
    public double StdCustomWordScore { get; set; }
    public double MaxCustomWordScore { get; set; }
    public double PercCustomScoreAboveSoftcapStart { get; set; }

    // Conjugation Stats
    public int TotalConjugations { get; set; }
    public Dictionary<string, int> ConjugationCategoryCounts { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, double> ConjugationCategoryRatios { get; set; } = new Dictionary<string, double>();
    public double RatioConjugations { get; set; }

    public ExtractedFeatures()
    {
        Ttr = 0;
        AverageSentenceLength = 0;
        KanjiRatio = 0;
        HiraganaRatio = 0;
        KatakanaRatio = 0;
        OtherRatio = 0;
        KanjiToKanaRatio = 0;
        LowFreqRankPerc = 0;
        LowFreqObsPerc = 0;
        PercCustomScoreAboveSoftcapStart = 0;
        RatioConjugations = 0;
        DialoguePercentage = 0;

        AvgLogFreqRank = double.NaN;
        MedianLogFreqRank = double.NaN;
        StdLogFreqRank = double.NaN;
        MinFreqRank = double.NaN;
        MaxFreqRank = double.NaN;
        AvgLogObsFreq = double.NaN;
        MedianLogObsFreq = double.NaN;
        StdLogObsFreq = double.NaN;
        MinObsFreq = double.NaN;
        MaxObsFreq = double.NaN;
        AvgReadingFreqRank = double.NaN;
        MedianReadingFreqRank = double.NaN;
        AvgReadingObsFreq = double.NaN;
        MedianReadingObsFreq = double.NaN;
        AvgReadingFreqPerc = double.NaN;
        MedianReadingFreqPerc = double.NaN;
        AvgCustomScorePerWord = double.NaN;
        MedianCustomWordScore = double.NaN;
        StdCustomWordScore = double.NaN;
        MaxCustomWordScore = double.NaN;

        foreach (var cat in MLConfig.ConjugationCategories.Keys)
        {
            ConjugationCategoryCounts[$"conj_{cat}_count"] = 0;
            ConjugationCategoryRatios[$"ratio_{cat}_conj"] = 0;
        }
    }
}