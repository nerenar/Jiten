using Jiten.Core.Data.JMDict;
using MessagePack;
using MessagePack.Resolvers;

namespace Jiten.Parser.Scoring;

internal readonly record struct RubyScoreResult(int Score, int Support, string? Level);

internal sealed class RubyReadingPriors
{
    private static readonly Lazy<RubyReadingPriors?> Instance =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static bool Enabled = true;

    public static RubyReadingPriors? Current => Enabled ? Instance.Value : null;

    private readonly Dictionary<string, Dictionary<string, int>> _unigrams;
    private readonly Dictionary<(string, string), Dictionary<string, int>> _leftBigrams;
    private readonly Dictionary<(string, string), Dictionary<string, int>> _rightBigrams;
    private readonly Dictionary<(string, string), Dictionary<string, int>> _left2Bigrams;
    private readonly Dictionary<(string, string), Dictionary<string, int>> _right2Bigrams;
    private readonly Dictionary<(string, string, string), Dictionary<string, int>> _trigrams;

    private readonly Dictionary<string, List<(string kanjiForm, int count)>> _reverseIndex;

    private RubyReadingPriors(
        Dictionary<string, Dictionary<string, int>> unigrams,
        Dictionary<(string, string), Dictionary<string, int>> leftBigrams,
        Dictionary<(string, string), Dictionary<string, int>> rightBigrams,
        Dictionary<(string, string), Dictionary<string, int>> left2Bigrams,
        Dictionary<(string, string), Dictionary<string, int>> right2Bigrams,
        Dictionary<(string, string, string), Dictionary<string, int>> trigrams)
    {
        _unigrams = unigrams;
        _leftBigrams = leftBigrams;
        _rightBigrams = rightBigrams;
        _left2Bigrams = left2Bigrams;
        _right2Bigrams = right2Bigrams;
        _trigrams = trigrams;

        _reverseIndex = new Dictionary<string, List<(string, int)>>();
        foreach (var (kanjiForm, readings) in _unigrams)
        {
            foreach (var (reading, count) in readings)
            {
                if (!_reverseIndex.TryGetValue(reading, out var list))
                {
                    list = [];
                    _reverseIndex[reading] = list;
                }
                list.Add((kanjiForm, count));
            }
        }

        foreach (var list in _reverseIndex.Values)
            list.Sort((a, b) => b.count.CompareTo(a.count));
    }

    private const double Alpha = 0.5;
    private const int MinUnigramTotal = 20;
    private const int MinBigramTotal = 8;
    private const int MinTrigramTotal = 5;
    private const double UnigramHalfLife = 50;
    private const double BigramHalfLife = 20;
    private const double TrigramHalfLife = 12;
    private const double ReliabilityThresholdTri = 0.30;
    private const double ReliabilityThresholdBi = 0.28;
    private const double RubyPriorWeight = 18;
    private const int MaxBonus = 35;
    private const int MaxPenalty = 20;
    private const double SupportReference = 50;

    public int ScoreCandidate(string? kanjiForm, string? reading, string? leftContext, string? rightContext,
        string? left2Context = null, string? right2Context = null)
        => ScoreCandidateDetailed(kanjiForm, reading, leftContext, rightContext, left2Context, right2Context).Score;

    public RubyScoreResult ScoreCandidateDetailed(string? kanjiForm, string? reading,
        string? leftContext, string? rightContext,
        string? left2Context = null, string? right2Context = null)
    {
        if (kanjiForm == null || reading == null) return default;
        if (!_unigrams.TryGetValue(kanjiForm, out var uniReadings)) return default;

        int uniTotal = 0;
        foreach (var c in uniReadings.Values) uniTotal += c;
        if (uniTotal < MinUnigramTotal) return default;

        int effectiveK = EffectiveReadingCount(uniReadings, uniTotal);
        double uniformLogP = Math.Log(1.0 / effectiveK);

        if (leftContext != null && rightContext != null)
        {
            var bestTri = TryTrigramExpanded(leftContext, kanjiForm, rightContext, reading, uniformLogP);
            if (bestTri.Level != null) return bestTri;
        }

        double? bestBiLogP = null;
        int bestBiTotal = 0;
        string? bestBiLevel = null;

        TryBigramExpanded(_leftBigrams, leftContext, kanjiForm, true,
            reading, "left-bigram", ref bestBiLogP, ref bestBiTotal, ref bestBiLevel);
        TryBigramExpanded(_rightBigrams, rightContext, kanjiForm, false,
            reading, "right-bigram", ref bestBiLogP, ref bestBiTotal, ref bestBiLevel);

        if (bestBiLogP.HasValue)
            return new RubyScoreResult(ComputeBonus(bestBiLogP.Value, uniformLogP, bestBiTotal), bestBiTotal, bestBiLevel);

        double? bestSkipLogP = null;
        int bestSkipTotal = 0;
        string? bestSkipLevel = null;

        TryBigramExpanded(_left2Bigrams, left2Context, kanjiForm, true,
            reading, "left2-bigram", ref bestSkipLogP, ref bestSkipTotal, ref bestSkipLevel);
        TryBigramExpanded(_right2Bigrams, right2Context, kanjiForm, false,
            reading, "right2-bigram", ref bestSkipLogP, ref bestSkipTotal, ref bestSkipLevel);

        if (bestSkipLogP.HasValue)
            return new RubyScoreResult(ComputeBonus(bestSkipLogP.Value, uniformLogP, bestSkipTotal), bestSkipTotal, bestSkipLevel);

        double uniLogP = Math.Log((uniReadings.GetValueOrDefault(reading) + Alpha) / (uniTotal + Alpha * effectiveK));
        return new RubyScoreResult(ComputeBonus(uniLogP, uniformLogP, uniTotal), uniTotal, "unigram");
    }

    private static bool ShouldExpandContext(string context)
    {
        if (context.Length < 2) return false;
        foreach (var c in context)
            if (c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿')
                  or (>= 'ァ' and <= 'ヶ') or 'ー')
                return false;
        return true;
    }

    private void TryBigramExpanded(
        Dictionary<(string, string), Dictionary<string, int>> table,
        string? context, string kanjiForm, bool contextIsLeft,
        string reading, string level,
        ref double? bestLogP, ref int bestTotal, ref string? bestLevel)
    {
        if (context == null) return;

        var key = contextIsLeft ? (context, kanjiForm) : (kanjiForm, context);
        TryBigram(table, key, reading, level, ref bestLogP, ref bestTotal, ref bestLevel);

        if (!ShouldExpandContext(context) || !_reverseIndex.TryGetValue(context, out var altForms))
            return;

        int tried = 0;
        foreach (var (altCtx, _) in altForms)
        {
            if (tried >= 3) break;
            var altKey = contextIsLeft ? (altCtx, kanjiForm) : (kanjiForm, altCtx);
            TryBigram(table, altKey, reading, level, ref bestLogP, ref bestTotal, ref bestLevel);
            tried++;
        }
    }

    private RubyScoreResult TryTrigramExpanded(
        string leftContext, string kanjiForm, string rightContext,
        string reading, double uniformLogP)
    {
        var best = default(RubyScoreResult);

        TryTrigramSingle(leftContext, kanjiForm, rightContext, reading, uniformLogP, ref best);

        if (ShouldExpandContext(leftContext) && _reverseIndex.TryGetValue(leftContext, out var leftAlts))
        {
            int tried = 0;
            foreach (var (altLeft, _) in leftAlts)
            {
                if (tried >= 3) break;
                TryTrigramSingle(altLeft, kanjiForm, rightContext, reading, uniformLogP, ref best);
                tried++;
            }
        }

        if (ShouldExpandContext(rightContext) && _reverseIndex.TryGetValue(rightContext, out var rightAlts))
        {
            int tried = 0;
            foreach (var (altRight, _) in rightAlts)
            {
                if (tried >= 3) break;
                TryTrigramSingle(leftContext, kanjiForm, altRight, reading, uniformLogP, ref best);
                tried++;
            }
        }

        return best;
    }

    private void TryTrigramSingle(
        string leftCtx, string kanjiForm, string rightCtx,
        string reading, double uniformLogP, ref RubyScoreResult best)
    {
        if (!_trigrams.TryGetValue((leftCtx, kanjiForm, rightCtx), out var triReadings))
            return;
        int triTotal = Sum(triReadings);
        double triReliability = (double)triTotal / (triTotal + TrigramHalfLife);
        if (triTotal < MinTrigramTotal || triReliability < ReliabilityThresholdTri)
            return;
        int triK = EffectiveReadingCount(triReadings, triTotal);
        double logP = Math.Log((triReadings.GetValueOrDefault(reading) + Alpha) / (triTotal + Alpha * triK));
        var score = ComputeBonus(logP, uniformLogP, triTotal);
        if (score > best.Score)
            best = new RubyScoreResult(score, triTotal, "trigram");
    }

    private void TryBigram(Dictionary<(string, string), Dictionary<string, int>> table,
        (string, string) key, string reading, string level,
        ref double? bestLogP, ref int bestTotal, ref string? bestLevel)
    {
        if (key.Item1 == null || key.Item2 == null) return;
        if (!table.TryGetValue(key, out var readings)) return;

        int total = Sum(readings);
        double reliability = (double)total / (total + BigramHalfLife);
        if (total < MinBigramTotal || reliability < ReliabilityThresholdBi) return;

        int k = EffectiveReadingCount(readings, total);
        double logP = Math.Log((readings.GetValueOrDefault(reading) + Alpha) / (total + Alpha * k));
        if (!bestLogP.HasValue || reliability > (double)bestTotal / (bestTotal + BigramHalfLife))
        {
            bestLogP = logP;
            bestTotal = total;
            bestLevel = level;
        }
    }

    private static int EffectiveReadingCount(Dictionary<string, int> readings, int total)
    {
        int threshold = Math.Max(10, (int)(total * 0.02));
        int count = 0;
        foreach (var v in readings.Values)
            if (v >= threshold) count++;
        return Math.Max(count, 2);
    }

    private static int ComputeBonus(double logP, double uniformLogP, int totalCount)
    {
        double rawBonus = RubyPriorWeight * (logP - uniformLogP);
        double supportScale = Math.Min(1.0, Math.Log(totalCount) / Math.Log(SupportReference));
        int effectiveMax = (int)(MaxBonus * supportScale);
        int effectiveMin = (int)(MaxPenalty * supportScale);
        return Math.Clamp((int)Math.Round(rawBonus), -effectiveMin, effectiveMax);
    }

    private static int Sum(Dictionary<string, int> dict)
    {
        int total = 0;
        foreach (var v in dict.Values) total += v;
        return total;
    }

    public int ScoreKanaReverse(string reading, JmDictWord word, string? leftContext, string? rightContext,
        string? left2Context = null, string? right2Context = null)
        => ScoreKanaReverseDetailed(reading, word, leftContext, rightContext, left2Context, right2Context).Score;

    public RubyScoreResult ScoreKanaReverseDetailed(string reading, JmDictWord word,
        string? leftContext, string? rightContext,
        string? left2Context = null, string? right2Context = null)
    {
        if (!_reverseIndex.TryGetValue(reading, out var allKanjiForms)) return default;
        if (leftContext == null && rightContext == null) return default;

        var best = default(RubyScoreResult);
        foreach (var form in word.Forms)
        {
            if (form.FormType != JmDictFormType.KanjiForm) continue;
            bool found = false;
            foreach (var (kanjiForm, _) in allKanjiForms)
            {
                if (kanjiForm == form.Text) { found = true; break; }
            }
            if (!found) continue;

            var result = ScoreCandidateDetailed(form.Text, reading,
                leftContext, rightContext, left2Context, right2Context);
            if (result.Level is not ("trigram" or "left-bigram" or "right-bigram"
                or "left2-bigram" or "right2-bigram"))
                continue;
            if (result.Score > best.Score)
                best = result;
        }

        return best;
    }

    internal static string ToHiragana(string text)
    {
        bool needsConversion = false;
        foreach (var c in text)
        {
            if (c >= 'ァ' && c <= 'ヶ') { needsConversion = true; break; }
        }
        if (!needsConversion) return text;

        return string.Create(text.Length, text, static (span, src) =>
        {
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                span[i] = (c >= 'ァ' && c <= 'ヶ') ? (char)(c - 0x60) : c;
            }
        });
    }

    public string? GetKanaReading(JmDictWord word, byte candidateReadingIndex)
    {
        if (word.Forms == null) return null;

        int kanaFormCount = 0;
        JmDictWordForm? firstKanaForm = null;
        JmDictWordForm? candidateForm = null;

        foreach (var form in word.Forms)
        {
            if (form.ReadingIndex == candidateReadingIndex)
                candidateForm = form;
            if (form.FormType == JmDictFormType.KanaForm)
            {
                kanaFormCount++;
                firstKanaForm ??= form;
            }
        }

        if (kanaFormCount == 0) return null;
        if (kanaFormCount == 1) return ToHiragana(firstKanaForm!.Text);

        if (candidateForm != null && candidateForm.FormType == JmDictFormType.KanaForm)
            return ToHiragana(candidateForm.Text);

        return ToHiragana(firstKanaForm!.Text);
    }

    public string? GetKanjiForm(JmDictWord word, string? surface = null)
    {
        if (word.Forms == null) return null;

        if (surface != null)
        {
            foreach (var form in word.Forms)
            {
                if (form.FormType == JmDictFormType.KanjiForm && form.Text == surface && _unigrams.ContainsKey(form.Text))
                    return form.Text;
            }
        }

        foreach (var form in word.Forms)
        {
            if (form.FormType == JmDictFormType.KanjiForm && _unigrams.ContainsKey(form.Text))
                return form.Text;
        }
        return null;
    }

    private static RubyReadingPriors? Load()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "ruby_reading_priors.msgpack");
        if (!File.Exists(path))
        {
            path = Path.Combine("Shared", "resources", "ruby_reading_priors.msgpack");
            if (!File.Exists(path))
            {
                path = Path.Combine("..", "Shared", "resources", "ruby_reading_priors.msgpack");
                if (!File.Exists(path)) return null;
            }
        }

        var bytes = File.ReadAllBytes(path);
        var options = ContractlessStandardResolver.Options;
        var raw = MessagePackSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, int>>>>(bytes, options);

        var leftBigrams = new Dictionary<(string, string), Dictionary<string, int>>();
        var rightBigrams = new Dictionary<(string, string), Dictionary<string, int>>();
        var left2Bigrams = new Dictionary<(string, string), Dictionary<string, int>>();
        var right2Bigrams = new Dictionary<(string, string), Dictionary<string, int>>();
        var trigrams = new Dictionary<(string, string, string), Dictionary<string, int>>();

        foreach (var (key, readings) in raw.GetValueOrDefault("LeftBigrams") ?? [])
        {
            var parts = key.Split('\t', 2);
            if (parts.Length == 2) leftBigrams[(parts[0], parts[1])] = readings;
        }

        foreach (var (key, readings) in raw.GetValueOrDefault("RightBigrams") ?? [])
        {
            var parts = key.Split('\t', 2);
            if (parts.Length == 2) rightBigrams[(parts[0], parts[1])] = readings;
        }

        foreach (var (key, readings) in raw.GetValueOrDefault("Left2Bigrams") ?? [])
        {
            var parts = key.Split('\t', 2);
            if (parts.Length == 2) left2Bigrams[(parts[0], parts[1])] = readings;
        }

        foreach (var (key, readings) in raw.GetValueOrDefault("Right2Bigrams") ?? [])
        {
            var parts = key.Split('\t', 2);
            if (parts.Length == 2) right2Bigrams[(parts[0], parts[1])] = readings;
        }

        foreach (var (key, readings) in raw.GetValueOrDefault("Trigrams") ?? [])
        {
            var parts = key.Split('\t', 3);
            if (parts.Length == 3) trigrams[(parts[0], parts[1], parts[2])] = readings;
        }

        return new RubyReadingPriors(
            raw.GetValueOrDefault("Unigrams") ?? new(),
            leftBigrams,
            rightBigrams,
            left2Bigrams,
            right2Bigrams,
            trigrams);
    }
}
