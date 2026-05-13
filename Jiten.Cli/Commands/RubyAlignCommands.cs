using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Jiten.Parser;
using Jiten.Parser.Diagnostics;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Cli.Commands;

public class RubyAlignCommands
{
    private readonly CliContext _context;

    private const string BOS = "<BOS>";
    private const string EOS = "<EOS>";
    private const string NUM = "<NUM>";

    public RubyAlignCommands(CliContext context)
    {
        _context = context;
    }

    public async Task AlignRuby(CliOptions options)
    {
        var inputPath = options.AlignRuby!;
        var outputPath = options.Output ?? "ruby_reading_priors.msgpack";
        var threads = Math.Max(1, options.Threads);

        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"ERROR: Input file not found: {inputPath}");
            return;
        }

        Console.WriteLine("Loading JMDict readings...");
        var (ambiguousForms, allKanjiReadings) = await LoadFormMaps();
        Console.WriteLine($"  {ambiguousForms.Count} ambiguous kanji forms (2+ readings)");
        Console.WriteLine($"  {allKanjiReadings.Count} total kanji forms with valid readings");

        Console.WriteLine($"Streaming alignment (threads={threads})...");
        var sw = Stopwatch.StartNew();
        var counts = await StreamAndAlign(inputPath, ambiguousForms, allKanjiReadings, outputPath, threads);
        Console.WriteLine($"  Completed in {sw.Elapsed.TotalSeconds:F1}s");

        PrintStats(counts);

        Console.WriteLine($"Serializing to {outputPath}...");
        SerializePriors(counts, outputPath);

        Console.WriteLine("Done.");
    }

    public async Task CheckRubyReadings(CliOptions options)
    {
        var inputPath = options.CheckRubyReadings!;
        var threads = Math.Max(1, options.Threads);
        var maxLines = 50_000;
        var priorsPath = options.Output ?? "ruby_reading_priors.msgpack";

        Console.WriteLine("Loading JMDict readings to identify ambiguous forms...");
        var ambiguousForms = (await LoadFormMaps()).ambiguous;

        RubyReadingPriorsData? priors = null;
        if (File.Exists(priorsPath))
        {
            Console.WriteLine($"Loading priors from {priorsPath}...");
            var bytes = await File.ReadAllBytesAsync(priorsPath);
            priors = MessagePackSerializer.Deserialize<RubyReadingPriorsData>(bytes, ContractlessStandardResolver.Options);
            Console.WriteLine($"  {priors.Unigrams.Count:N0} unigrams, {priors.LeftBigrams.Count:N0} left bi, {priors.RightBigrams.Count:N0} right bi, {priors.Trigrams.Count:N0} trigrams");
        }

        Console.WriteLine($"Reading up to {maxLines:N0} lines from {inputPath}...");
        var lines = new List<string>();
        using (var reader = new StreamReader(inputPath, Encoding.UTF8))
        {
            while (lines.Count < maxLines && await reader.ReadLineAsync() is { } line)
            {
                if (line.Length < 10) continue;
                RubyExtractCommands.RubySentence? s;
                try { s = JsonSerializer.Deserialize(line, SentenceJsonContext.Default.RubySentence); }
                catch { continue; }
                if (s?.R.Any(a => ambiguousForms.Contains(a.B)) == true)
                    lines.Add(line);
            }
        }

        Console.WriteLine($"  {lines.Count:N0} sentences with ambiguous ruby");
        Console.WriteLine($"Parsing and comparing readings (threads={threads})...");

        long agree = 0, disagree = 0, noMatch = 0;

        // N-gram correction stats (for disagreement cases)
        long uniCorrect = 0, uniWrong = 0, uniMissing = 0;
        long leftBiCorrect = 0, leftBiWrong = 0, leftBiMissing = 0;
        long rightBiCorrect = 0, rightBiWrong = 0, rightBiMissing = 0;
        long triCorrect = 0, triWrong = 0, triMissing = 0;

        // Context distance analysis: where is the discriminative token?
        // Track left1/left2/right1/right2 tokens for disagreement cases
        var contextTokenDistances = new ConcurrentBag<(string target, string ruby, string left1, string left2, string right1, string right2)>();

        var sw = Stopwatch.StartNew();
        var batchSize = Math.Max(50, lines.Count / threads);
        var batches = new List<List<string>>();
        for (int i = 0; i < lines.Count; i += batchSize)
            batches.Add(lines.GetRange(i, Math.Min(batchSize, lines.Count - i)));

        await Parallel.ForEachAsync(batches,
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (batch, ct) =>
            {
                var analyser = new MorphologicalAnalyser();

                foreach (var line in batch)
                {
                    RubyExtractCommands.RubySentence? sentence;
                    try { sentence = JsonSerializer.Deserialize(line, SentenceJsonContext.Default.RubySentence); }
                    catch { continue; }
                    if (sentence == null) continue;

                    List<SentenceInfo> parsed;
                    try { parsed = await analyser.Parse(sentence.S); }
                    catch { continue; }

                    var tokens = parsed
                        .SelectMany(si => si.Words.Select(w => w.word))
                        .Where(w => !w.IsInvalid)
                        .ToList();

                    foreach (var ann in sentence.R.Where(a => ambiguousForms.Contains(a.B)))
                    {
                        int exactIdx = -1, exactCount = 0;
                        for (int i = 0; i < tokens.Count; i++)
                        {
                            if (tokens[i].DictionaryForm == ann.B || tokens[i].Text == ann.B)
                            {
                                exactIdx = i;
                                exactCount++;
                            }
                        }

                        if (exactCount != 1) { Interlocked.Increment(ref noMatch); continue; }

                        var token = tokens[exactIdx];
                        if (token.Text != token.DictionaryForm) { Interlocked.Increment(ref noMatch); continue; }

                        var sudachiReading = ToHiragana(token.Reading);
                        var rubyReading = ann.R;

                        if (sudachiReading == rubyReading)
                        {
                            Interlocked.Increment(ref agree);
                            continue;
                        }

                        Interlocked.Increment(ref disagree);

                        if (priors == null) continue;

                        var target = ann.B;
                        var left1 = FindContextKey(tokens, exactIdx, -1);
                        var right1 = FindContextKey(tokens, exactIdx, +1);
                        var left2 = left1 == BOS ? BOS : FindContextKeyN(tokens, exactIdx, -1, 2);
                        var right2 = right1 == EOS ? EOS : FindContextKeyN(tokens, exactIdx, +1, 2);

                        contextTokenDistances.Add((target, rubyReading, left1, left2, right1, right2));

                        // Check unigram
                        if (priors.Unigrams.TryGetValue(target, out var uniReadings))
                        {
                            var best = uniReadings.MaxBy(r => r.Value).Key;
                            if (best == rubyReading) Interlocked.Increment(ref uniCorrect);
                            else Interlocked.Increment(ref uniWrong);
                        }
                        else Interlocked.Increment(ref uniMissing);

                        // Check left bigram
                        var lbKey = $"{left1}\t{target}";
                        if (priors.LeftBigrams.TryGetValue(lbKey, out var lbReadings))
                        {
                            var best = lbReadings.MaxBy(r => r.Value).Key;
                            if (best == rubyReading) Interlocked.Increment(ref leftBiCorrect);
                            else Interlocked.Increment(ref leftBiWrong);
                        }
                        else Interlocked.Increment(ref leftBiMissing);

                        // Check right bigram
                        var rbKey = $"{target}\t{right1}";
                        if (priors.RightBigrams.TryGetValue(rbKey, out var rbReadings))
                        {
                            var best = rbReadings.MaxBy(r => r.Value).Key;
                            if (best == rubyReading) Interlocked.Increment(ref rightBiCorrect);
                            else Interlocked.Increment(ref rightBiWrong);
                        }
                        else Interlocked.Increment(ref rightBiMissing);

                        // Check trigram
                        var triKey = $"{left1}\t{target}\t{right1}";
                        if (priors.Trigrams.TryGetValue(triKey, out var triReadings))
                        {
                            var best = triReadings.MaxBy(r => r.Value).Key;
                            if (best == rubyReading) Interlocked.Increment(ref triCorrect);
                            else Interlocked.Increment(ref triWrong);
                        }
                        else Interlocked.Increment(ref triMissing);
                    }
                }
            });

        var total = agree + disagree;
        Console.WriteLine($"\nDone in {sw.Elapsed.TotalSeconds:F1}s");
        Console.WriteLine($"\n=== Sudachi Reading vs Ruby Reading ===");
        Console.WriteLine($"Compared: {total:N0} (skipped {noMatch:N0} no-unique-match)");
        Console.WriteLine($"Agree:    {agree:N0} ({(total > 0 ? 100.0 * agree / total : 0):F1}%)");
        Console.WriteLine($"Disagree: {disagree:N0} ({(total > 0 ? 100.0 * disagree / total : 0):F1}%)");

        if (priors != null && disagree > 0)
        {
            Console.WriteLine($"\n=== N-gram Correction of Sudachi Errors (n={disagree}) ===");
            Console.WriteLine("  {0,-12} {1,8} {2,8} {3,8} {4,6} {5,6}",
                "Level", "Correct", "Wrong", "Missing", "Hit%", "Acc%");
            PrintRow("Unigram", uniCorrect, uniWrong, uniMissing);
            PrintRow("Left-bi", leftBiCorrect, leftBiWrong, leftBiMissing);
            PrintRow("Right-bi", rightBiCorrect, rightBiWrong, rightBiMissing);
            PrintRow("Trigram", triCorrect, triWrong, triMissing);

            // Analyze where discriminative context sits
            // For each disagreement case, check if our left1/right1 bigrams have the right answer
            // but wider context might add more
            Console.WriteLine($"\n=== Context Distance Analysis ===");
            Console.WriteLine($"  For {contextTokenDistances.Count:N0} Sudachi errors:");
            var items = contextTokenDistances.ToList();

            // Group by whether left/right bigram has the answer
            int leftBiHas = 0, rightBiHas = 0, triHas = 0, noneHas = 0;
            foreach (var (target, ruby, l1, l2, r1, r2) in items)
            {
                bool lb = priors.LeftBigrams.TryGetValue($"{l1}\t{target}", out var lbr) && lbr.MaxBy(r => r.Value).Key == ruby;
                bool rb = priors.RightBigrams.TryGetValue($"{target}\t{r1}", out var rbr) && rbr.MaxBy(r => r.Value).Key == ruby;
                bool tri = priors.Trigrams.TryGetValue($"{l1}\t{target}\t{r1}", out var trr) && trr.MaxBy(r => r.Value).Key == ruby;

                if (tri) triHas++;
                else if (lb || rb) { if (lb) leftBiHas++; else rightBiHas++; }
                else noneHas++;
            }
            Console.WriteLine($"  Trigram corrects:             {triHas:N0} ({100.0 * triHas / items.Count:F1}%)");
            Console.WriteLine($"  Bigram corrects (tri fails):  {leftBiHas + rightBiHas:N0} ({100.0 * (leftBiHas + rightBiHas) / items.Count:F1}%)");
            Console.WriteLine($"  Neither corrects:             {noneHas:N0} ({100.0 * noneHas / items.Count:F1}%)");

            // For the "neither corrects" cases, show what left2/right2 look like
            var neitherCases = items.Where(x =>
            {
                bool lb = priors.LeftBigrams.TryGetValue($"{x.left1}\t{x.target}", out var lbr) && lbr.MaxBy(r => r.Value).Key == x.ruby;
                bool rb = priors.RightBigrams.TryGetValue($"{x.target}\t{x.right1}", out var rbr) && rbr.MaxBy(r => r.Value).Key == x.ruby;
                bool tri = priors.Trigrams.TryGetValue($"{x.left1}\t{x.target}\t{x.right1}", out var trr) && trr.MaxBy(r => r.Value).Key == x.ruby;
                return !lb && !rb && !tri;
            }).ToList();

            Console.WriteLine($"\n  'Neither corrects' breakdown ({neitherCases.Count} cases):");
            var neitherByTarget = neitherCases.GroupBy(x => x.target)
                .Select(g => (target: g.Key, count: g.Count(), sample: g.First()))
                .OrderByDescending(x => x.count)
                .Take(20);
            foreach (var (target, count, sample) in neitherByTarget)
            {
                Console.WriteLine($"    {target} x{count}: ruby={sample.ruby}, ctx=[{sample.left2}] [{sample.left1}] ___ [{sample.right1}] [{sample.right2}]");
            }
        }

        static void PrintRow(string label, long correct, long wrong, long missing)
        {
            var hits = correct + wrong;
            var total = correct + wrong + missing;
            var hitPct = total > 0 ? 100.0 * hits / total : 0;
            var accPct = hits > 0 ? 100.0 * correct / hits : 0;
            Console.WriteLine("  {0,-12} {1,8:N0} {2,8:N0} {3,8:N0} {4,5:F1}% {5,5:F1}%",
                label, correct, wrong, missing, hitPct, accPct);
        }
    }

    public async Task RubyChangedWinnerReport(CliOptions options)
    {
        var inputPath = options.RubyChangedWinners!;
        var outputPath = options.Output;
        var maxSentences = 5_000;

        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"ERROR: Input file not found: {inputPath}");
            return;
        }

        Console.WriteLine("Loading JMDict readings for ambiguous forms...");
        var ambiguousForms = (await LoadFormMaps()).ambiguous;

        Console.WriteLine("Loading word form readings for candidate resolution...");
        var readingCache = await LoadReadingCache();

        Console.WriteLine("Loading name forms for filtering...");
        var nameForms = await LoadNameForms();

        Console.WriteLine($"Reading sentences from {inputPath}...");
        var allCandidates = new List<(string text, List<RubyExtractCommands.RubyAnnotation> annotations)>();
        using (var reader = new StreamReader(inputPath, Encoding.UTF8))
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.Length < 10) continue;
                RubyExtractCommands.RubySentence? s;
                try { s = JsonSerializer.Deserialize(line, SentenceJsonContext.Default.RubySentence); }
                catch { continue; }
                if (s == null) continue;
                var ambiguous = s.R.Where(a => ambiguousForms.Contains(a.B) && !nameForms.Contains(a.B)).ToList();
                if (ambiguous.Count > 0)
                    allCandidates.Add((s.S, ambiguous));
            }
        }

        Console.WriteLine($"  {allCandidates.Count:N0} eligible sentences (names excluded)");

        var rng = new Random(42);
        var sentences = allCandidates.OrderBy(_ => rng.Next()).Take(maxSentences).ToList();

        Console.WriteLine($"  {sentences.Count:N0} sentences with ambiguous ruby annotations");

        // Phase 1: parse with ruby priors enabled
        Console.WriteLine("Pass 1/2: parsing with ruby priors enabled...");
        Jiten.Parser.Parser.RubyPriorsEnabled = true;
        var sw = Stopwatch.StartNew();

        var rubyResults = new List<List<DeckWord>>();
        var rubyDiagnostics = new List<ParserDiagnostics>();
        for (int i = 0; i < sentences.Count; i++)
        {
            var diag = new ParserDiagnostics { InputText = sentences[i].text };
            try
            {
                var result = await Jiten.Parser.Parser.ParseText(_context.ContextFactory, sentences[i].text, diagnostics: diag);
                rubyResults.Add(result);
                rubyDiagnostics.Add(diag);
            }
            catch
            {
                rubyResults.Add([]);
                rubyDiagnostics.Add(diag);
            }
            if ((i + 1) % 1000 == 0)
                Console.WriteLine($"  {i + 1:N0}/{sentences.Count:N0}...");
        }
        Console.WriteLine($"  Pass 1 done in {sw.Elapsed.TotalSeconds:F0}s");

        // Phase 2: parse with ruby priors disabled (baseline)
        Console.WriteLine("Pass 2/2: parsing with ruby priors disabled (baseline)...");
        Jiten.Parser.Parser.RubyPriorsEnabled = false;
        sw.Restart();

        var baselineResults = new List<List<DeckWord>>();
        for (int i = 0; i < sentences.Count; i++)
        {
            try
            {
                var result = await Jiten.Parser.Parser.ParseText(_context.ContextFactory, sentences[i].text);
                baselineResults.Add(result);
            }
            catch
            {
                baselineResults.Add([]);
            }
            if ((i + 1) % 1000 == 0)
                Console.WriteLine($"  {i + 1:N0}/{sentences.Count:N0}...");
        }
        Console.WriteLine($"  Pass 2 done in {sw.Elapsed.TotalSeconds:F0}s");

        Jiten.Parser.Parser.RubyPriorsEnabled = true;

        // Compare results
        int totalEvaluated = 0, totalChanged = 0;
        int rubyCorrect = 0, rubyRegressed = 0, bothWrong = 0;
        var changedEntries = new List<ChangedWinnerEntry>();
        var targetStats = new Dictionary<string, (int changed, int correct)>();

        for (int i = 0; i < sentences.Count; i++)
        {
            var (text, annotations) = sentences[i];
            var rubyTokens = rubyResults[i];
            var baselineTokens = baselineResults[i];
            var diag = rubyDiagnostics[i];

            foreach (var ann in annotations)
            {
                var rubyMatch = rubyTokens.FirstOrDefault(t => t.OriginalText == ann.B);
                var baselineMatch = baselineTokens.FirstOrDefault(t => t.OriginalText == ann.B);
                if (rubyMatch == null || baselineMatch == null) continue;

                totalEvaluated++;

                bool changed = rubyMatch.WordId != baselineMatch.WordId ||
                               rubyMatch.ReadingIndex != baselineMatch.ReadingIndex;
                if (!changed) continue;

                totalChanged++;

                var actualReading = ResolveReading(readingCache, rubyMatch.WordId, rubyMatch.ReadingIndex);
                var baselineReading = ResolveReading(readingCache, baselineMatch.WordId, baselineMatch.ReadingIndex);
                var rubyAnnotation = ann.R;

                string category;
                if (actualReading == rubyAnnotation)
                {
                    category = "ruby_correct";
                    rubyCorrect++;
                }
                else if (baselineReading == rubyAnnotation)
                {
                    category = "ruby_regressed";
                    rubyRegressed++;
                }
                else
                {
                    category = "both_wrong";
                    bothWrong++;
                }

                if (!targetStats.TryGetValue(ann.B, out var stats))
                    stats = (0, 0);
                targetStats[ann.B] = (stats.changed + 1, stats.correct + (category == "ruby_correct" ? 1 : 0));

                // Get ruby detail from diagnostics
                var wordResult = diag.Results.FirstOrDefault(r => r.Text == ann.B || r.DictionaryForm == ann.B);
                var selectedCandidate = wordResult?.Candidates?.FirstOrDefault(c => c.IsSelected);

                changedEntries.Add(new ChangedWinnerEntry
                {
                    Sentence = text,
                    Target = ann.B,
                    BaselineReading = baselineReading ?? "?",
                    ActualReading = actualReading ?? "?",
                    RubyAnnotation = rubyAnnotation,
                    Category = category,
                    BaselineScore = 0,
                    ActualScore = selectedCandidate?.TotalScore ?? 0,
                    RubyPriorsScore = selectedCandidate?.RubyPriorsScore ?? 0,
                    RubyPriorLevel = selectedCandidate?.RubyPriorLevel,
                    RubyPriorSupport = selectedCandidate?.RubyPriorSupport,
                    Margin = wordResult?.MarginToSecond
                });
            }
        }

        Console.WriteLine($"\n=== Ruby Prior Changed-Winner Report ===");
        Console.WriteLine($"Sentences parsed:            {sentences.Count:N0}");
        Console.WriteLine($"Ambiguous tokens evaluated:  {totalEvaluated:N0}");
        Console.WriteLine($"Changed winners:             {totalChanged:N0}");

        if (totalChanged > 0)
        {
            Console.WriteLine($"\nChanged-winner accuracy:");
            Console.WriteLine($"  Ruby correct (fixed baseline):   {rubyCorrect,5:N0} ({100.0 * rubyCorrect / totalChanged:F1}%)");
            Console.WriteLine($"  Ruby regressed (broke baseline): {rubyRegressed,5:N0} ({100.0 * rubyRegressed / totalChanged:F1}%)");
            Console.WriteLine($"  Both wrong (different):          {bothWrong,5:N0} ({100.0 * bothWrong / totalChanged:F1}%)");

            Console.WriteLine($"\nTop changed targets:");
            foreach (var (target, (changed, correct)) in targetStats
                         .OrderByDescending(kv => kv.Value.changed)
                         .Take(30))
            {
                Console.WriteLine($"  {target,-8} x{changed,3}: ruby correct {correct}/{changed}");
            }

            var regressions = changedEntries.Where(e => e.Category == "ruby_regressed")
                .OrderByDescending(e => Math.Abs(e.RubyPriorsScore))
                .Take(20)
                .ToList();
            if (regressions.Count > 0)
            {
                Console.WriteLine($"\nTop regressions (ruby broke baseline):");
                foreach (var r in regressions)
                    Console.WriteLine($"  {r.Target}: {r.BaselineReading}→{r.ActualReading} (ruby={r.RubyAnnotation}, score={r.RubyPriorsScore}, {r.RubyPriorLevel}) \"{TruncateSentence(r.Sentence, 60)}\"");
            }
        }

        if (outputPath != null)
        {
            var jsonOptions = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            foreach (var entry in changedEntries.OrderBy(e => e.Target).ThenBy(e => e.Category))
                await writer.WriteLineAsync(JsonSerializer.Serialize(entry, jsonOptions));
            Console.WriteLine($"\nDetailed JSONL written to {outputPath} ({changedEntries.Count:N0} entries)");
        }
    }

    private async Task<HashSet<string>> LoadNameForms()
    {
        await using var db = _context.ContextFactory.CreateDbContext();

        var words = await db.JMDictWords
            .AsNoTracking()
            .Select(w => new { w.WordId, w.PartsOfSpeech })
            .ToListAsync();

        var nameWordIds = words
            .Where(w => w.PartsOfSpeech.Any(p => RubyExtractCommands.NamePosTags.Contains(p)))
            .Select(w => w.WordId)
            .ToHashSet();

        var nameForms = await db.WordForms
            .AsNoTracking()
            .Where(f => nameWordIds.Contains(f.WordId) && f.FormType == JmDictFormType.KanjiForm && !f.IsObsolete)
            .Select(f => f.Text)
            .Distinct()
            .ToListAsync();

        return nameForms.ToHashSet();
    }

    private async Task<Dictionary<int, List<(short readingIndex, JmDictFormType formType, string text)>>> LoadReadingCache()
    {
        await using var db = _context.ContextFactory.CreateDbContext();
        var forms = await db.WordForms
            .AsNoTracking()
            .Where(f => !f.IsObsolete && !f.IsSearchOnly)
            .Select(f => new { f.WordId, f.ReadingIndex, f.FormType, f.Text })
            .ToListAsync();

        return forms
            .GroupBy(f => f.WordId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => (f.ReadingIndex, f.FormType, f.Text)).ToList());
    }

    private static string? ResolveReading(
        Dictionary<int, List<(short readingIndex, JmDictFormType formType, string text)>> cache,
        int wordId, byte readingIndex)
    {
        if (!cache.TryGetValue(wordId, out var forms)) return null;

        var kanaForms = forms.Where(f => f.formType == JmDictFormType.KanaForm).ToList();
        if (kanaForms.Count == 0) return null;
        if (kanaForms.Count == 1) return ToHiragana(kanaForms[0].text);

        var candidate = forms.FirstOrDefault(f => f.readingIndex == readingIndex);
        if (candidate.formType == JmDictFormType.KanaForm)
            return ToHiragana(candidate.text);

        return ToHiragana(kanaForms[0].text);
    }

    private static string TruncateSentence(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";

    private class ChangedWinnerEntry
    {
        public string Sentence { get; set; } = "";
        public string Target { get; set; } = "";
        public string BaselineReading { get; set; } = "";
        public string ActualReading { get; set; } = "";
        public string RubyAnnotation { get; set; } = "";
        public string Category { get; set; } = "";
        public int BaselineScore { get; set; }
        public int ActualScore { get; set; }
        public int RubyPriorsScore { get; set; }
        public string? RubyPriorLevel { get; set; }
        public int? RubyPriorSupport { get; set; }
        public int? Margin { get; set; }
    }

    private static string FindContextKeyN(List<WordInfo> tokens, int fromIdx, int direction, int n)
    {
        int found = 0;
        for (int i = fromIdx + direction; i >= 0 && i < tokens.Count; i += direction)
        {
            var t = tokens[i];
            if (t.PartOfSpeech is PartOfSpeech.Symbol or PartOfSpeech.SupplementarySymbol)
                continue;
            found++;
            if (found < n) continue;
            if (t.PartOfSpeech == PartOfSpeech.Numeral) return NUM;
            return !string.IsNullOrEmpty(t.DictionaryForm) ? t.DictionaryForm : t.Text;
        }
        return direction < 0 ? BOS : EOS;
    }

    private async Task<(HashSet<string> ambiguous, Dictionary<string, HashSet<string>> all)> LoadFormMaps()
    {
        await using var db = _context.ContextFactory.CreateDbContext();

        var words = await db.JMDictWords
            .AsNoTracking()
            .Select(w => new { w.WordId, w.PartsOfSpeech })
            .ToListAsync();

        var nameWordIds = words
            .Where(w => w.PartsOfSpeech.Any(p => RubyExtractCommands.NamePosTags.Contains(p)))
            .Select(w => w.WordId)
            .ToHashSet();

        var forms = await db.WordForms
            .AsNoTracking()
            .Where(f => !f.IsObsolete && !f.IsSearchOnly)
            .Select(f => new { f.WordId, f.FormType, f.Text })
            .ToListAsync();

        var kanjiByWord = forms
            .Where(f => f.FormType == JmDictFormType.KanjiForm && !nameWordIds.Contains(f.WordId))
            .GroupBy(f => f.WordId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Text).ToList());

        var kanaByWord = forms
            .Where(f => f.FormType == JmDictFormType.KanaForm)
            .GroupBy(f => f.WordId)
            .ToDictionary(g => g.Key, g => g.Select(f => ToHiragana(f.Text)).ToHashSet());

        var readingsByKanji = new Dictionary<string, HashSet<string>>();
        foreach (var (wordId, kanjiTexts) in kanjiByWord)
        {
            if (!kanaByWord.TryGetValue(wordId, out var readings)) continue;
            foreach (var kanji in kanjiTexts)
            {
                if (!readingsByKanji.TryGetValue(kanji, out var existing))
                {
                    existing = new HashSet<string>();
                    readingsByKanji[kanji] = existing;
                }
                existing.UnionWith(readings);
            }
        }

        var readingAmbiguous = readingsByKanji
            .Where(kvp => kvp.Value.Count >= 2)
            .Select(kvp => kvp.Key)
            .ToHashSet();

        // Also mark kanji forms as ambiguous if their reading is shared by other kanji forms
        // (kana-homophone-ambiguous: 聞く/効く/利く all share reading きく)
        var kanjiFormsByReading = new Dictionary<string, HashSet<string>>();
        foreach (var (kanjiForm, readings) in readingsByKanji)
        {
            foreach (var reading in readings)
            {
                if (!kanjiFormsByReading.TryGetValue(reading, out var formSet))
                {
                    formSet = new HashSet<string>();
                    kanjiFormsByReading[reading] = formSet;
                }
                formSet.Add(kanjiForm);
            }
        }

        var kanaAmbiguous = new HashSet<string>();
        foreach (var (_, formSet) in kanjiFormsByReading)
        {
            if (formSet.Count >= 2)
                kanaAmbiguous.UnionWith(formSet);
        }

        var ambiguous = new HashSet<string>(readingAmbiguous);
        ambiguous.UnionWith(kanaAmbiguous);

        Console.WriteLine($"  Reading-ambiguous: {readingAmbiguous.Count:N0}, kana-homophone-ambiguous: {kanaAmbiguous.Count:N0} (union: {ambiguous.Count:N0})");

        return (ambiguous, readingsByKanji);
    }

    private async Task<AlignmentCounts> StreamAndAlign(
        string inputPath, HashSet<string> ambiguousForms,
        Dictionary<string, HashSet<string>> allKanjiReadings,
        string outputPath, int threads)
    {
        var globalCounts = new AlignmentCounts();
        var heldOutPath = Path.ChangeExtension(outputPath, ".heldout.jsonl");

        long totalLines = 0;
        long skipped = 0;
        long trainCount = 0;
        long heldOutCount = 0;
        long totalAligned = 0;
        long totalFailed = 0;
        long reverseOnlyCount = 0;
        const int chunkSize = 5000;

        using var heldOutWriter = new StreamWriter(heldOutPath, false, Encoding.UTF8);
        var heldOutLock = new object();

        using var reader = new StreamReader(inputPath, Encoding.UTF8);
        var chunk = new List<string>(chunkSize);

        while (true)
        {
            chunk.Clear();
            while (chunk.Count < chunkSize)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                totalLines++;
                if (totalLines % 5_000_000 == 0)
                    Console.WriteLine($"    {totalLines:N0} lines scanned, {trainCount:N0} train, {totalAligned:N0} aligned, {reverseOnlyCount:N0} reverse-only...");

                if (line.Length < 10) continue;

                RubyExtractCommands.RubySentence? sentence;
                try
                {
                    sentence = JsonSerializer.Deserialize(line, SentenceJsonContext.Default.RubySentence);
                }
                catch { continue; }

                if (sentence == null || sentence.R.Count == 0) { skipped++; continue; }

                int h = 0;
                foreach (var c in sentence.Src) h = h * 31 + c;
                bool isHeldOut = Math.Abs(h % 10) == 0;

                if (isHeldOut)
                {
                    Interlocked.Increment(ref heldOutCount);
                    lock (heldOutLock) heldOutWriter.WriteLine(line);
                    continue;
                }

                Interlocked.Increment(ref trainCount);
                chunk.Add(line);
            }

            if (chunk.Count == 0) break;

            var batchSize = Math.Max(50, chunk.Count / threads);
            var batches = new List<List<string>>();
            for (int i = 0; i < chunk.Count; i += batchSize)
            {
                var end = Math.Min(i + batchSize, chunk.Count);
                batches.Add(chunk.GetRange(i, end - i));
            }

            var localCountsBag = new ConcurrentBag<AlignmentCounts>();

            await Parallel.ForEachAsync(batches,
                new ParallelOptions { MaxDegreeOfParallelism = threads },
                async (batch, ct) =>
                {
                    var analyser = new MorphologicalAnalyser();
                    var localCounts = new AlignmentCounts();
                    long localAligned = 0;
                    long localFailed = 0;

                    foreach (var line in batch)
                    {
                        RubyExtractCommands.RubySentence? sentence;
                        try
                        {
                            sentence = JsonSerializer.Deserialize(line,
                                SentenceJsonContext.Default.RubySentence);
                        }
                        catch { continue; }

                        if (sentence == null || sentence.R.Count == 0) continue;

                        bool needsParsing = sentence.R.Any(a => ambiguousForms.Contains(a.B));

                        // Non-ambiguous unigrams: count without parsing
                        foreach (var ann in sentence.R)
                        {
                            if (ambiguousForms.Contains(ann.B)) continue;
                            if (allKanjiReadings.TryGetValue(ann.B, out var validReadings) && validReadings.Contains(ann.R))
                            {
                                localCounts.AddUnigram(ann.B, ann.R);
                                Interlocked.Increment(ref reverseOnlyCount);
                            }
                        }

                        if (!needsParsing) continue;

                        List<SentenceInfo> parsed;
                        try
                        {
                            parsed = await analyser.Parse(sentence.S);
                        }
                        catch { continue; }

                        var tokens = parsed
                            .SelectMany(si => si.Words.Select(w => w.word))
                            .Where(w => !w.IsInvalid)
                            .ToList();

                        if (tokens.Count == 0) continue;

                        foreach (var ann in sentence.R)
                        {
                            bool isAmbiguous = ambiguousForms.Contains(ann.B);
                            if (!isAmbiguous && !(allKanjiReadings.TryGetValue(ann.B, out var vr) && vr.Contains(ann.R)))
                                continue;

                            var (matchIdx, matchCount) = FindTokenMatch(tokens, ann.B);
                            if (matchCount == 0)
                            {
                                if (isAmbiguous) localFailed++;
                                continue;
                            }

                            if (isAmbiguous)
                            {
                                localAligned++;
                                localCounts.AddUnigram(ann.B, ann.R);
                            }

                            if (matchCount == 1)
                            {
                                var left = FindContextKey(tokens, matchIdx, -1);
                                var right = FindContextKey(tokens, matchIdx, +1);
                                localCounts.AddLeftBigram(left, ann.B, ann.R);
                                localCounts.AddRightBigram(ann.B, right, ann.R);
                                localCounts.AddTrigram(left, ann.B, right, ann.R);

                                var left2 = FindContextKeyN(tokens, matchIdx, -1, 2);
                                var right2 = FindContextKeyN(tokens, matchIdx, +1, 2);
                                if (left2 != left) localCounts.AddLeft2Bigram(left2, ann.B, ann.R);
                                if (right2 != right) localCounts.AddRight2Bigram(ann.B, right2, ann.R);
                            }
                        }
                    }

                    Interlocked.Add(ref totalAligned, localAligned);
                    Interlocked.Add(ref totalFailed, localFailed);
                    localCountsBag.Add(localCounts);
                });

            foreach (var local in localCountsBag)
                globalCounts.MergeFrom(local);
        }

        Console.WriteLine($"  Lines: {totalLines:N0} total, {skipped:N0} skipped, {trainCount:N0} train, {heldOutCount:N0} held-out");
        Console.WriteLine($"  Aligned (ambiguous): {totalAligned:N0}, Failed: {totalFailed:N0} ({(totalAligned + totalFailed > 0 ? 100.0 * totalAligned / (totalAligned + totalFailed) : 0):F1}%)");
        Console.WriteLine($"  Non-ambiguous unigrams: {reverseOnlyCount:N0}");

        return globalCounts;
    }

    private static (int index, int count) FindTokenMatch(List<WordInfo> tokens, string dictForm)
    {
        int matchIdx = -1;
        int matchCount = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].DictionaryForm == dictForm ||
                tokens[i].Text == dictForm)
            {
                matchIdx = i;
                matchCount++;
            }
        }

        return (matchIdx, matchCount);
    }

    private static string FindContextKey(List<WordInfo> tokens, int fromIdx, int direction)
    {
        for (int i = fromIdx + direction; i >= 0 && i < tokens.Count; i += direction)
        {
            var t = tokens[i];
            if (t.PartOfSpeech is PartOfSpeech.Symbol or PartOfSpeech.SupplementarySymbol)
                continue;
            if (t.PartOfSpeech == PartOfSpeech.Numeral)
                return NUM;
            return !string.IsNullOrEmpty(t.DictionaryForm) ? t.DictionaryForm : t.Text;
        }
        return direction < 0 ? BOS : EOS;
    }

    private static void PrintStats(AlignmentCounts counts)
    {
        Console.WriteLine("\n--- Alignment Statistics ---");
        Console.WriteLine($"  Unigram targets:     {counts.Unigrams.Count:N0}");
        Console.WriteLine($"  Left bigram keys:    {counts.LeftBigrams.Count:N0}");
        Console.WriteLine($"  Right bigram keys:   {counts.RightBigrams.Count:N0}");
        Console.WriteLine($"  Left-2 bigram keys:  {counts.Left2Bigrams.Count:N0}");
        Console.WriteLine($"  Right-2 bigram keys: {counts.Right2Bigrams.Count:N0}");
        Console.WriteLine($"  Trigram keys:        {counts.Trigrams.Count:N0}");

        long totalUnigramObs = counts.Unigrams.Values.Sum(d => d.Values.Sum(v => (long)v));
        Console.WriteLine($"  Total unigram obs:   {totalUnigramObs:N0}");

        var topTargets = counts.Unigrams
            .Select(kvp => (target: kvp.Key, total: kvp.Value.Values.Sum(), readings: kvp.Value))
            .OrderByDescending(x => x.total)
            .Take(20)
            .ToList();

        Console.WriteLine("\n  Top 20 ambiguous targets:");
        foreach (var (target, total, readings) in topTargets)
        {
            var readingStr = string.Join(", ",
                readings.OrderByDescending(r => r.Value)
                    .Select(r => $"{r.Key}:{r.Value}"));
            Console.WriteLine($"    {target} ({total:N0}): {readingStr}");
        }

        var balanced = counts.Unigrams
            .Where(kvp =>
            {
                var total = kvp.Value.Values.Sum();
                if (total < 100) return false;
                var minority = kvp.Value.Values.Min();
                return (double)minority / total >= 0.05;
            })
            .Count();
        Console.WriteLine($"\n  Balanced targets (minority≥5%, total≥100): {balanced}");
    }

    private static void SerializePriors(AlignmentCounts counts, string outputPath)
    {
        const int uniMinCount = 5;
        const int biMinCount = 15;
        const int skipMinCount = 22;

        var data = new RubyReadingPriorsData
        {
            Unigrams = PruneTable(counts.Unigrams, uniMinCount),
            LeftBigrams = PruneTable(counts.LeftBigrams, biMinCount),
            RightBigrams = PruneTable(counts.RightBigrams, biMinCount),
            Left2Bigrams = PruneTable(counts.Left2Bigrams, skipMinCount),
            Right2Bigrams = PruneTable(counts.Right2Bigrams, skipMinCount),
            Trigrams = PruneTable(counts.Trigrams, biMinCount)
        };

        Console.WriteLine($"  After pruning (uni≥{uniMinCount}, bi≥{biMinCount}, skip≥{skipMinCount}):");
        Console.WriteLine($"    Unigrams: {data.Unigrams.Count:N0}, Left-bi: {data.LeftBigrams.Count:N0}, Right-bi: {data.RightBigrams.Count:N0}");
        Console.WriteLine($"    Left2-bi: {data.Left2Bigrams.Count:N0}, Right2-bi: {data.Right2Bigrams.Count:N0}, Trigrams: {data.Trigrams.Count:N0}");

        var options = ContractlessStandardResolver.Options;
        var bytes = MessagePackSerializer.Serialize(data, options);
        File.WriteAllBytes(outputPath, bytes);
        Console.WriteLine($"  Written {bytes.Length:N0} bytes ({bytes.Length / 1024.0 / 1024.0:F1} MB)");
    }

    public static void RepruneArtifact(string inputPath, string? outputPath)
    {
        Console.WriteLine($"Loading {inputPath}...");
        var bytes = File.ReadAllBytes(inputPath);
        var options = ContractlessStandardResolver.Options;
        var data = MessagePackSerializer.Deserialize<RubyReadingPriorsData>(bytes, options);
        Console.WriteLine($"  Original: {bytes.Length / 1024.0 / 1024.0:F1} MB");
        PrintTableSizes("  Original counts", data);

        int[] thresholds = [5, 8, 10, 15, 20, 30];
        foreach (var t in thresholds)
        {
            var pruned = new RubyReadingPriorsData
            {
                Unigrams = PruneTable(data.Unigrams, Math.Min(t, 5)),
                LeftBigrams = PruneTable(data.LeftBigrams, t),
                RightBigrams = PruneTable(data.RightBigrams, t),
                Left2Bigrams = PruneTable(data.Left2Bigrams, (int)(t * 1.5)),
                Right2Bigrams = PruneTable(data.Right2Bigrams, (int)(t * 1.5)),
                Trigrams = PruneTable(data.Trigrams, t)
            };
            var prunedBytes = MessagePackSerializer.Serialize(pruned, options);
            Console.WriteLine($"\n  Threshold bi={t} skip={t * 1.5:F0} (uni=min(5,{t})):");
            Console.WriteLine($"    Size: {prunedBytes.Length / 1024.0 / 1024.0:F1} MB");
            PrintTableSizes("    Counts", pruned);
        }

        if (outputPath != null)
        {
            Console.Write("\nEnter bigram threshold to use for output (e.g. 10): ");
            // Use a reasonable default for non-interactive mode
            int biThreshold = 15;
            var final = new RubyReadingPriorsData
            {
                Unigrams = PruneTable(data.Unigrams, 5),
                LeftBigrams = PruneTable(data.LeftBigrams, biThreshold),
                RightBigrams = PruneTable(data.RightBigrams, biThreshold),
                Left2Bigrams = PruneTable(data.Left2Bigrams, (int)(biThreshold * 1.5)),
                Right2Bigrams = PruneTable(data.Right2Bigrams, (int)(biThreshold * 1.5)),
                Trigrams = PruneTable(data.Trigrams, biThreshold)
            };
            var finalBytes = MessagePackSerializer.Serialize(final, options);
            File.WriteAllBytes(outputPath, finalBytes);
            Console.WriteLine($"\n  Written {outputPath}: {finalBytes.Length / 1024.0 / 1024.0:F1} MB");
            PrintTableSizes("  Final counts", final);
        }
    }

    private static void PrintTableSizes(string label, RubyReadingPriorsData data)
    {
        Console.WriteLine($"{label}: uni={data.Unigrams.Count:N0} L={data.LeftBigrams.Count:N0} R={data.RightBigrams.Count:N0} L2={data.Left2Bigrams.Count:N0} R2={data.Right2Bigrams.Count:N0} tri={data.Trigrams.Count:N0}");
    }

    private static Dictionary<string, Dictionary<string, int>> PruneTable(
        Dictionary<string, Dictionary<string, int>> table, int minCount)
    {
        var pruned = new Dictionary<string, Dictionary<string, int>>();
        foreach (var (key, readings) in table)
        {
            var filtered = new Dictionary<string, int>();
            foreach (var (reading, count) in readings)
                if (count >= minCount) filtered[reading] = count;
            if (filtered.Count > 0)
                pruned[key] = filtered;
        }
        return pruned;
    }

    private static string ToHiragana(string katakana)
    {
        var sb = new StringBuilder(katakana.Length);
        foreach (var c in katakana)
        {
            if (c >= 'ァ' && c <= 'ヶ')
                sb.Append((char)(c - 0x60));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    private class AlignmentCounts
    {
        public Dictionary<string, Dictionary<string, int>> Unigrams { get; } = new();
        public Dictionary<string, Dictionary<string, int>> LeftBigrams { get; } = new();
        public Dictionary<string, Dictionary<string, int>> RightBigrams { get; } = new();
        public Dictionary<string, Dictionary<string, int>> Left2Bigrams { get; } = new();
        public Dictionary<string, Dictionary<string, int>> Right2Bigrams { get; } = new();
        public Dictionary<string, Dictionary<string, int>> Trigrams { get; } = new();

        public void AddUnigram(string target, string reading)
            => AddToTable(Unigrams, target, reading);

        public void AddLeftBigram(string left, string target, string reading)
            => AddToTable(LeftBigrams, $"{left}\t{target}", reading);

        public void AddRightBigram(string target, string right, string reading)
            => AddToTable(RightBigrams, $"{target}\t{right}", reading);

        public void AddLeft2Bigram(string left2, string target, string reading)
            => AddToTable(Left2Bigrams, $"{left2}\t{target}", reading);

        public void AddRight2Bigram(string target, string right2, string reading)
            => AddToTable(Right2Bigrams, $"{target}\t{right2}", reading);

        public void AddTrigram(string left, string target, string right, string reading)
            => AddToTable(Trigrams, $"{left}\t{target}\t{right}", reading);

        private static void AddToTable(Dictionary<string, Dictionary<string, int>> table, string key, string reading)
        {
            if (!table.TryGetValue(key, out var readings))
            {
                readings = new Dictionary<string, int>();
                table[key] = readings;
            }
            readings[reading] = readings.GetValueOrDefault(reading) + 1;
        }

        public void MergeFrom(AlignmentCounts other)
        {
            MergeTable(Unigrams, other.Unigrams);
            MergeTable(LeftBigrams, other.LeftBigrams);
            MergeTable(RightBigrams, other.RightBigrams);
            MergeTable(Left2Bigrams, other.Left2Bigrams);
            MergeTable(Right2Bigrams, other.Right2Bigrams);
            MergeTable(Trigrams, other.Trigrams);
        }

        private static void MergeTable(
            Dictionary<string, Dictionary<string, int>> target,
            Dictionary<string, Dictionary<string, int>> source)
        {
            foreach (var (key, readings) in source)
            {
                if (!target.TryGetValue(key, out var existing))
                {
                    existing = new Dictionary<string, int>();
                    target[key] = existing;
                }
                foreach (var (reading, count) in readings)
                    existing[reading] = existing.GetValueOrDefault(reading) + count;
            }
        }
    }
}

public class RubyReadingPriorsData
{
    public Dictionary<string, Dictionary<string, int>> Unigrams { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> LeftBigrams { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> RightBigrams { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> Left2Bigrams { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> Right2Bigrams { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> Trigrams { get; set; } = new();
}
