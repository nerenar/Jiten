using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using Jiten.Core;
using Jiten.Core.Data;

namespace Jiten.Parser;

public readonly record struct SubtitleStats(long MoraCount, long DurationMs)
{
    public static readonly SubtitleStats Empty = new(0, 0);

    public double MoraPerMinute => DurationMs > 0 ? MoraCount / (DurationMs / 60000.0) : 0;
}

public static class SubtitleMoraRateCalculator
{
    // Drop elongation tildes that follow kana so they do not count toward mora.
    private static readonly Regex KanaTildeRegex =
        new(@"(?<=[\u3040-\u309F\u30A0-\u30FF])[～〜]+", RegexOptions.Compiled);
    private const string SmallKanaChars = "ぁぃぅぇぉゃゅょゎゕゖァィゥェォャュョヮヵヶ";

    public static async Task<SubtitleStats> ComputeAsync(IEnumerable<SubtitleItem> items)
    {
        var entries = new List<SubtitleEntry>();

        foreach (var item in items)
        {
            if (!TryGetSpokenText(item.Text, out var spoken))
                continue;

            if (item.EndMs <= item.StartMs)
                continue;

            entries.Add(new SubtitleEntry(item.StartMs, item.EndMs, spoken, item.TrackIndex));
        }

        if (entries.Count == 0)
            return SubtitleStats.Empty;

        var moraCounts = await CountMoraPerTextAsync(entries.Select(e => e.Text).ToList());
        if (moraCounts.Count == 0)
            return SubtitleStats.Empty;

        var rateEntries = new List<SubtitleRateEntry>(entries.Count);
        for (int i = 0; i < entries.Count && i < moraCounts.Count; i++)
        {
            var moraCount = moraCounts[i];
            if (moraCount <= 0)
                continue;

            var entry = entries[i];
            var durationMs = entry.EndMs - entry.StartMs;
            if (durationMs <= 0)
                continue;

            var rate = moraCount / (durationMs / 60000.0);
            rateEntries.Add(new SubtitleRateEntry(entry.StartMs, entry.EndMs, moraCount, rate, entry.TrackIndex));
        }

        if (rateEntries.Count == 0)
            return SubtitleStats.Empty;

        rateEntries = TrimOutliers(rateEntries);
        if (rateEntries.Count == 0)
            return SubtitleStats.Empty;

        var totalMora = rateEntries.Sum(e => e.MoraCount);
        var durationMsTotal = MergeIntervalsPerTrack(rateEntries);

        return new SubtitleStats(totalMora, durationMsTotal);
    }

    private static bool TryGetSpokenText(string rawText, out string spoken)
    {
        spoken = string.Empty;

        var cleaned = SubtitleTextCleaner.CleanText(rawText);
        if (string.IsNullOrWhiteSpace(cleaned))
            return false;

        var stripped = SubtitleTextCleaner.StripNonSpoken(cleaned);
        if (string.IsNullOrWhiteSpace(stripped))
            return false;

        stripped = KanaTildeRegex.Replace(stripped, "");
        if (string.IsNullOrWhiteSpace(stripped))
            return false;

        spoken = stripped;
        return true;
    }

    private const int BatchSize = 200;

    private static async Task<List<long>> CountMoraPerTextAsync(IReadOnlyList<string> texts)
    {
        if (texts.Count == 0)
            return [];

        var parser = new MorphologicalAnalyser();
        var counts = new List<long>(texts.Count);

        for (int offset = 0; offset < texts.Count; offset += BatchSize)
        {
            var chunk = texts.Skip(offset).Take(BatchSize).ToList();
            var batches = await parser.ParseBatch(chunk, morphemesOnly: true);

            foreach (var sentences in batches)
            {
                long count = 0;
                foreach (var sentence in sentences)
                {
                    foreach (var (word, _, _) in sentence.Words)
                    {
                        count += CountMora(word);
                    }
                }

                counts.Add(count);
            }
        }

        return counts;
    }

    private static int CountMora(WordInfo word)
    {
        if (word.PartOfSpeech is PartOfSpeech.Symbol or PartOfSpeech.SupplementarySymbol or PartOfSpeech.BlankSpace)
            return 0;

        var reading = GetReadingOrSurface(word);

        var count = 0;
        foreach (var rune in reading.EnumerateRunes())
        {
            if (IsMoraKana(rune))
                count++;
        }

        return count;
    }

    private static string GetReadingOrSurface(WordInfo word)
    {
        var reading = word.Reading;
        return string.IsNullOrEmpty(reading) || reading == "*" ? word.Text : reading;
    }

    private static bool IsMoraKana(Rune rune)
    {
        if (!IsKana(rune))
            return false;
        if (IsSmallKana(rune))
            return false;
        return true;
    }

    private static bool IsKana(Rune rune)
    {
        return IsInRange(rune, UnicodeRanges.Hiragana) || IsInRange(rune, UnicodeRanges.Katakana);
    }

    private static bool IsSmallKana(Rune rune)
    {
        return rune.Value <= char.MaxValue && SmallKanaChars.Contains((char)rune.Value);
    }

    private static bool IsInRange(Rune rune, UnicodeRange range)
    {
        var start = range.FirstCodePoint;
        var end = start + range.Length - 1;
        var value = rune.Value;
        return value >= start && value <= end;
    }

    private static long MergeIntervalsPerTrack(List<SubtitleRateEntry> entries)
    {
        long total = 0;

        foreach (var group in entries.GroupBy(e => e.TrackIndex))
        {
            var sorted = group
                .Where(e => e.EndMs > e.StartMs)
                .OrderBy(e => e.StartMs)
                .ToList();

            if (sorted.Count == 0)
                continue;

            int mergedStart = sorted[0].StartMs, mergedEnd = sorted[0].EndMs;

            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].StartMs <= mergedEnd)
                {
                    mergedEnd = Math.Max(mergedEnd, sorted[i].EndMs);
                }
                else
                {
                    total += mergedEnd - mergedStart;
                    mergedStart = sorted[i].StartMs;
                    mergedEnd = sorted[i].EndMs;
                }
            }

            total += mergedEnd - mergedStart;
        }

        return total;
    }

    private static List<SubtitleRateEntry> TrimOutliers(List<SubtitleRateEntry> entries)
    {
        if (entries.Count < 4)
            return entries;

        var rates = entries.Select(e => e.Rate).OrderBy(r => r).ToList();
        var q1 = Percentile(rates, 25);
        var q3 = Percentile(rates, 75);
        var iqr = q3 - q1;
        if (iqr <= 0)
            return entries;

        var lower = q1 - 1.5 * iqr;
        var upper = q3 + 1.5 * iqr;
        return entries.Where(e => e.Rate >= lower && e.Rate <= upper).ToList();
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;
        if (percentile <= 0)
            return sortedValues[0];
        if (percentile >= 100)
            return sortedValues[^1];

        var k = (sortedValues.Count - 1) * (percentile / 100.0);
        var f = (int)Math.Floor(k);
        var c = Math.Min(f + 1, sortedValues.Count - 1);
        if (f == c)
            return sortedValues[f];

        return sortedValues[f] * (c - k) + sortedValues[c] * (k - f);
    }

    private readonly record struct SubtitleEntry(int StartMs, int EndMs, string Text, int TrackIndex);
    private readonly record struct SubtitleRateEntry(int StartMs, int EndMs, long MoraCount, double Rate, int TrackIndex);
}
