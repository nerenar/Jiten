using System.Text;
using Jiten.Core;
using Jiten.Core.Data;

namespace Jiten.Cli.ML;

public static class MLHelper
{
    public static void ExtractCharacterCounts(string text, ExtractedFeatures features)
    {
        features.TotalCount = text.Length;
        if (features.TotalCount == 0) return;

        foreach (Rune rune in text.EnumerateRunes())
        {
            int codePoint = rune.Value;

            switch (codePoint)
            {
                case >= 0x4E00 and <= 0x9FFF: // Common Kanji
                case >= 0x3400 and <= 0x4DBF: // Rare Kanji A
                case >= 0x20000 and <= 0x2A6DF: // Rare Kanji B (Supplementary Ideographic Plane)
                case >= 0x2A700 and <= 0x2B73F: // Rare Kanji C
                    features.KanjiCount++;
                    break;
                case >= 0x3040 and <= 0x309F: // Hiragana
                    features.HiraganaCount++;
                    break;
                case >= 0x30A0 and <= 0x30FF: // Katakana
                    features.KatakanaCount++;
                    break;
                default:
                    features.OtherCount++;
                    break;
            }
        }

        features.KanjiRatio = (double)features.KanjiCount / features.TotalCount;
        features.HiraganaRatio = (double)features.HiraganaCount / features.TotalCount;
        features.KatakanaRatio = (double)features.KatakanaCount / features.TotalCount;
        features.OtherRatio = (double)features.OtherCount / features.TotalCount;
        double kanaAndOther = features.HiraganaCount + features.KatakanaCount + features.OtherCount;
        features.KanjiToKanaRatio = kanaAndOther > 0 ? (double)features.KanjiCount / kanaAndOther : 0;
    }

    public static async Task ExtractFrequencyStats(JitenDbContext context, List<DeckWord> deckWords, ExtractedFeatures features)
    {
        context = new JitenDbContext(context.DbOptions);
        if (!deckWords.Any()) return;

        var wordIdsToExclude = context.JMDictWords.Where(w => w.Priorities != null && w.Priorities.Contains("name")).Select(w => w.WordId).ToList();
        var wordIds = deckWords.Select(dw => dw.WordId).Where(w => !wordIdsToExclude.Contains(w)).Distinct();


        if (!wordIds.Any()) return;

        var freqDataMap = context.JmDictWordFrequencies
                                 .Where(f => wordIds.Contains(f.WordId))
                                 .ToDictionary(f => f.WordId);

        List<double> freqRanks = new List<double>();
        List<double> obsFreqs = new List<double>();
        List<double> readingFreqRanks = new List<double>();
        List<double> readingObsFreqs = new List<double>();
        List<double> readingFreqPercentages = new List<double>();
        List<double> customWordScores = new List<double>();

        long totalWordOccurrences = 0;
        long lowFreqRankCount = 0;
        long lowFreqObsCount = 0;
        long customScoreAboveSoftcapStartCount = 0;
        double scoreAtSoftcapStartThreshold = CalculateCustomRankScore(MLConfig.RankSoftcapStart);
        double totalCustomScoreSum = 0;

        foreach (var dw in deckWords)
        {
            totalWordOccurrences += dw.Occurrences;

            if (!freqDataMap.TryGetValue(dw.WordId, out var freqData)) continue;

            double customScore = CalculateCustomRankScore(freqData.FrequencyRank);
            if (!double.IsNaN(customScore))
            {
                customWordScores.AddRange(Enumerable.Repeat(customScore, dw.Occurrences));
                totalCustomScoreSum += customScore * dw.Occurrences;
                if (customScore >= scoreAtSoftcapStartThreshold)
                    customScoreAboveSoftcapStartCount += dw.Occurrences;
            }

            freqRanks.AddRange(Enumerable.Repeat((double)freqData.FrequencyRank, dw.Occurrences));
            if (freqData.FrequencyRank > MLConfig.LowFreqRankThreshold) lowFreqRankCount += dw.Occurrences;

            obsFreqs.AddRange(Enumerable.Repeat(freqData.ObservedFrequency, dw.Occurrences));
            if (freqData.ObservedFrequency < MLConfig.LowFreqObserverThreshold) lowFreqObsCount += dw.Occurrences;

            int rIdx = dw.ReadingIndex;
            if (rIdx < freqData.ReadingsFrequencyRank.Count)
                readingFreqRanks.AddRange(Enumerable.Repeat((double)freqData.ReadingsFrequencyRank[rIdx], dw.Occurrences));
            if (rIdx < freqData.ReadingsObservedFrequency.Count)
                readingObsFreqs.AddRange(Enumerable.Repeat(freqData.ReadingsObservedFrequency[rIdx], dw.Occurrences));
            if (rIdx < freqData.ReadingsFrequencyPercentage.Count)
                readingFreqPercentages.AddRange(Enumerable.Repeat(freqData.ReadingsFrequencyPercentage[rIdx], dw.Occurrences));
        }

        if (totalWordOccurrences == 0) return;

        // Log transform and calculate stats
        var logFreqRanks = freqRanks.Any() ? freqRanks.Select(r => Math.Log(r + 1e-9)).ToList() : new List<double>();
        var logObsFreqs = obsFreqs.Any() ? obsFreqs.Select(f => Math.Log(f + 1e-9)).ToList() : new List<double>();
        // Npgsql might return int[] for rank arrays, ensure they are double for log.
        var logReadingFreqRanks = readingFreqRanks.Any() ? readingFreqRanks.Select(r => Math.Log(r + 1e-9)).ToList() : new List<double>();
        var logReadingObsFreqs = readingObsFreqs.Any() ? readingObsFreqs.Select(f => Math.Log(f + 1e-9)).ToList() : new List<double>();


        features.AvgLogFreqRank = SafeAverage(logFreqRanks);
        features.MedianLogFreqRank = SafeMedian(logFreqRanks);
        features.StdLogFreqRank = SafeStdDev(logFreqRanks);
        features.MinFreqRank = freqRanks.Any() ? freqRanks.Min() : double.NaN;
        features.MaxFreqRank = freqRanks.Any() ? freqRanks.Max() : double.NaN;

        features.AvgLogObsFreq = SafeAverage(logObsFreqs);
        features.MedianLogObsFreq = SafeMedian(logObsFreqs);
        features.StdLogObsFreq = SafeStdDev(logObsFreqs);
        features.MinObsFreq = obsFreqs.Any() ? obsFreqs.Min() : double.NaN;
        features.MaxObsFreq = obsFreqs.Any() ? obsFreqs.Max() : double.NaN;

        features.LowFreqRankPerc = (double)lowFreqRankCount / totalWordOccurrences;
        features.LowFreqObsPerc = (double)lowFreqObsCount / totalWordOccurrences;

        features.AvgReadingFreqRank = SafeAverage(logReadingFreqRanks);
        features.MedianReadingFreqRank = SafeMedian(logReadingFreqRanks);
        features.AvgReadingObsFreq = SafeAverage(logReadingObsFreqs);
        features.MedianReadingObsFreq = SafeMedian(logReadingObsFreqs);
        features.AvgReadingFreqPerc = SafeAverage(readingFreqPercentages);
        features.MedianReadingFreqPerc = SafeMedian(readingFreqPercentages);

        features.AvgCustomScorePerWord = totalWordOccurrences > 0 ? totalCustomScoreSum / totalWordOccurrences : double.NaN;
        features.MedianCustomWordScore = SafeMedian(customWordScores);
        features.StdCustomWordScore = SafeStdDev(customWordScores);
        features.MaxCustomWordScore = customWordScores.Any() ? customWordScores.Max() : double.NaN;
        features.PercCustomScoreAboveSoftcapStart = (double)customScoreAboveSoftcapStartCount / totalWordOccurrences;
    }

    public static void ExtractConjugationStats(List<DeckWord> deckWords, ExtractedFeatures features)
    {
        if (!deckWords.Any()) return;

        var categoryCounts = new Dictionary<string, int>();
        foreach (var cat in MLConfig.ConjugationCategories.Keys)
        {
            categoryCounts[cat] = 0;
        }

        int totalConjugations = 0;

        foreach (var dw in deckWords)
        {
            if (dw.Conjugations.Count == 0) continue;
            foreach (var conjDetail in dw.Conjugations)
            {
                totalConjugations += dw.Occurrences;
                string category = MLConfig.ConjugationDetailToCategory.TryGetValue(conjDetail, out var catVal) ? catVal : "other";
                categoryCounts[category] += dw.Occurrences;
            }
        }

        features.TotalConjugations = totalConjugations;
        foreach (var cat in MLConfig.ConjugationCategories.Keys) // Use defined categories to ensure all are present
        {
            string countKey = $"conj_{cat.Replace("/", "_")}_count"; // Sanitize key for CsvHelper
            string ratioKey = $"ratio_{cat.Replace("/", "_")}_conj";

            features.ConjugationCategoryCounts[countKey] = categoryCounts.GetValueOrDefault(cat, 0);
            features.ConjugationCategoryRatios[ratioKey] =
                totalConjugations > 0 ? (double)features.ConjugationCategoryCounts[countKey] / totalConjugations : 0;
        }

        if (features.WordCount > 0)
        {
            features.RatioConjugations = (double)totalConjugations / features.WordCount;
        }
        else
        {
            features.RatioConjugations = 0;
        }
    }

    private static double SafeAverage(List<double> list) => list.Any() ? list.Average() : double.NaN;

    private static double SafeMedian(List<double> list)
    {
        if (!list.Any()) return double.NaN;
        var sorted = list.OrderBy(x => x).ToList();
        int mid = sorted.Count / 2;
        return (sorted.Count % 2 != 0) ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static double SafeStdDev(List<double> list)
    {
        if (list.Count < 2) return double.NaN; // StdDev not meaningful for < 2 items
        double avg = list.Average();
        double sumOfSquares = list.Sum(val => (val - avg) * (val - avg));
        return Math.Sqrt(sumOfSquares / (list.Count - 1)); // Sample StDev
    }

    private static double CalculateCustomRankScore(int? rankNullable)
    {
        if (!rankNullable.HasValue)
        {
            return double.NaN;
        }

        int rank = rankNullable.Value;

        if (rank <= MLConfig.RankBase) return (double)rank;
        if (rank >= MLConfig.RankHardcap) return MLConfig.ScoreHardcapValue;

        double score = MLConfig.RankBase;
        int currentRankThreshold = MLConfig.RankBase;
        double multiplier = 1.0;

        while (currentRankThreshold < MLConfig.RankSoftcapStart && currentRankThreshold < rank)
        {
            multiplier *= 2;
            int nextThreshold = currentRankThreshold + MLConfig.RankExpBase;
            int rankInThisStep = Math.Min(rank, nextThreshold) - currentRankThreshold;
            score += rankInThisStep * multiplier;
            currentRankThreshold = nextThreshold;
            if (rank <= currentRankThreshold) return score;
        }

        double multiplierAtSoftcap = multiplier;
        while (currentRankThreshold < MLConfig.RankHardcap && currentRankThreshold < rank)
        {
            int nextThreshold = currentRankThreshold + MLConfig.RankSoftcapStep;
            int rankInThisStep = Math.Min(rank, nextThreshold) - currentRankThreshold;
            score += rankInThisStep * multiplierAtSoftcap;
            currentRankThreshold = nextThreshold;
            if (rank <= currentRankThreshold) return score;
        }

        return score;
    }
}