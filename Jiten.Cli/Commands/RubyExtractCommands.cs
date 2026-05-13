using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Jiten.Core;
using Jiten.Core.Data.JMDict;
using Jiten.Parser;
using Microsoft.EntityFrameworkCore;
using VersOne.Epub;
using VersOne.Epub.Options;

namespace Jiten.Cli.Commands;

public class RubyExtractCommands
{
    private readonly CliContext _context;

    private static readonly string[] BannedChapterNames =
        ["cover", "toc", "fmatter", "credit", "illust", "colophon"];

    private static readonly string[] BoilerplateKeywords =
    [
        "ISBN", "©", "発行", "印刷", "出版", "CONTENTS", "目次", "初版", "奥付",
        "無断で複製", "第三者に譲渡", "転載", "配信", "電子書籍", "プリント版"
    ];

    internal static readonly HashSet<string> NamePosTags =
        ["name", "surname", "place", "person", "given", "unclass"];

    public RubyExtractCommands(CliContext context)
    {
        _context = context;
    }

    public async Task ExtractRuby(CliOptions options)
    {
        var dirs = options.ExtractRuby!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Console.WriteLine("Scanning directories for EPUB files...");
        var epubPaths = new List<string>();
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"  WARNING: Directory not found: {dir}");
                continue;
            }
            var files = Directory.GetFiles(dir, "*.epub", SearchOption.AllDirectories);
            Console.WriteLine($"  {dir}: {files.Length} EPUBs");
            epubPaths.AddRange(files);
        }

        Console.WriteLine($"Total: {epubPaths.Count} EPUBs to process");
        if (epubPaths.Count == 0) return;

        Console.WriteLine("Loading JMDict readings...");
        var (jmdictMap, nameSet) = await LoadJmDictReadings();
        Console.WriteLine($"  Loaded {jmdictMap.Count} kanji forms with readings, {nameSet.Count} name forms");

        var bookStats = new ConcurrentBag<BookStats>();
        var globalPairs = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
        var sentencesPath = options.Output != null
            ? Path.ChangeExtension(options.Output, ".jsonl")
            : "ruby_sentences.jsonl";
        int processed = 0;
        int failed = 0;

        Console.WriteLine($"Extracting ruby (threads={options.Threads})...");
        Console.WriteLine($"Sentences output: {sentencesPath}");

        using var sentenceWriter = new StreamWriter(sentencesPath, false, Encoding.UTF8);
        var writeLock = new object();

        await Parallel.ForEachAsync(epubPaths,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.Threads) },
            async (path, ct) =>
            {
                try
                {
                    var (stats, sentences) = await ProcessEpub(path, globalPairs, jmdictMap);
                    if (stats != null)
                        bookStats.Add(stats);

                    if (sentences.Count > 0)
                    {
                        var lines = new StringBuilder();
                        foreach (var sent in sentences)
                            lines.AppendLine(JsonSerializer.Serialize(sent, SentenceJsonContext.Default.RubySentence));

                        lock (writeLock)
                            sentenceWriter.Write(lines);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                }

                var p = Interlocked.Increment(ref processed);
                if (p % 500 == 0)
                    Console.WriteLine($"  {p}/{epubPaths.Count} processed...");
            });

        Console.WriteLine($"\nDone. {processed} processed, {failed} failed.\n");

        OutputReport(bookStats, globalPairs, jmdictMap, nameSet, options.Output);
    }

    private async Task<(Dictionary<string, HashSet<string>> map, HashSet<string> names)> LoadJmDictReadings()
    {
        await using var db = _context.ContextFactory.CreateDbContext();

        var forms = await db.WordForms
            .AsNoTracking()
            .Where(f => !f.IsObsolete && !f.IsSearchOnly)
            .Select(f => new { f.WordId, f.FormType, f.Text })
            .ToListAsync();

        var words = await db.JMDictWords
            .AsNoTracking()
            .Select(w => new { w.WordId, w.PartsOfSpeech })
            .ToListAsync();

        var nameWordIds = words
            .Where(w => w.PartsOfSpeech.Any(p => NamePosTags.Contains(p)))
            .Select(w => w.WordId)
            .ToHashSet();

        var kanjiByWord = forms
            .Where(f => f.FormType == JmDictFormType.KanjiForm)
            .GroupBy(f => f.WordId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Text).ToList());

        var kanaByWord = forms
            .Where(f => f.FormType == JmDictFormType.KanaForm)
            .GroupBy(f => f.WordId)
            .ToDictionary(g => g.Key, g => g.Select(f => ToHiragana(f.Text)).ToHashSet());

        var result = new Dictionary<string, HashSet<string>>();
        var nameSet = new HashSet<string>();

        foreach (var (wordId, kanjiTexts) in kanjiByWord)
        {
            if (!kanaByWord.TryGetValue(wordId, out var readings)) continue;
            var isName = nameWordIds.Contains(wordId);

            foreach (var kanji in kanjiTexts)
            {
                if (isName) nameSet.Add(kanji);

                if (!result.TryGetValue(kanji, out var existing))
                {
                    existing = new HashSet<string>();
                    result[kanji] = existing;
                }
                existing.UnionWith(readings);
            }
        }

        return (result, nameSet);
    }

    private async Task<(BookStats? stats, List<RubySentence> sentences)> ProcessEpub(
        string path,
        ConcurrentDictionary<string, ConcurrentDictionary<string, long>> globalPairs,
        Dictionary<string, HashSet<string>> jmdictMap)
    {
        var chapters = await ReadEpubChapters(path);
        if (chapters == null || chapters.Count == 0) return (null, []);

        var parser = new HtmlParser();
        int totalKanjiChars = 0;
        int totalRubySpans = 0;
        int totalChars = 0;
        var allSentences = new List<RubySentence>();
        var sourceHash = path.GetHashCode().ToString("x8");

        foreach (var (key, html) in chapters)
        {
            var doc = await parser.ParseDocumentAsync(PreprocessHtml(html));
            var body = doc.Body;
            if (body == null) continue;

            var rubyElements = body.QuerySelectorAll("ruby").ToList();
            var chapterSpans = new List<ResolvedRubySpan>();

            foreach (var rubyEl in rubyElements)
            {
                var (baseText, reading, okurigana) = ExtractRubyPair(rubyEl);
                if (string.IsNullOrWhiteSpace(baseText) || string.IsNullOrWhiteSpace(reading))
                    continue;

                if (!HasKanji(baseText)) continue;

                var normalizedReading = ToHiragana(reading.Trim());
                var normalizedBase = baseText.Trim();

                if (normalizedReading.Length == 0) continue;
                if (!IsPlausibleReading(normalizedReading)) continue;

                var finalBase = normalizedBase;
                var finalReading = normalizedReading;
                bool matched = false;
                if (okurigana.Length > 0)
                {
                    for (int okuLen = okurigana.Length; okuLen >= 1; okuLen--)
                    {
                        var oku = okurigana[..okuLen];
                        var extBase = normalizedBase + oku;
                        var extReading = normalizedReading + oku;
                        if (TryMatchReading(jmdictMap, extBase, extReading, out var okuMatch))
                        {
                            finalBase = extBase;
                            finalReading = okuMatch;
                            matched = true;
                            break;
                        }
                    }
                }
                if (!matched && TryMatchReading(jmdictMap, finalBase, finalReading, out var baseMatch))
                {
                    finalReading = baseMatch;
                    matched = true;
                }
                if (!matched && okurigana.Length > 0)
                {
                    var fullReading = normalizedReading + okurigana;
                    var (deconBase, deconReading) = TryDeconjugateMatch(
                        jmdictMap, normalizedBase, normalizedReading, fullReading);
                    if (deconBase != null)
                    {
                        finalBase = deconBase;
                        finalReading = deconReading!;
                    }
                }

                totalRubySpans++;

                var globalReadings = globalPairs.GetOrAdd(finalBase, _ => new ConcurrentDictionary<string, long>());
                globalReadings.AddOrUpdate(finalReading, 1, (_, old) => old + 1);

                chapterSpans.Add(new ResolvedRubySpan
                {
                    Surface = normalizedBase,
                    DictForm = finalBase,
                    Reading = finalReading
                });

                rubyEl.Parent?.ReplaceChild(doc.CreateTextNode(normalizedBase), rubyEl);
            }

            var plainText = body.TextContent;
            totalChars += plainText.Length;
            foreach (var c in plainText)
            {
                if (IsKanji(c)) totalKanjiChars++;
            }

            if (chapterSpans.Count > 0)
                ExtractSentences(plainText, chapterSpans, sourceHash, allSentences);
        }

        return (new BookStats
        {
            Path = path,
            TotalChars = totalChars,
            TotalKanjiChars = totalKanjiChars,
            RubySpanCount = totalRubySpans,
            CoverageRatio = totalKanjiChars > 0 ? (double)totalRubySpans / totalKanjiChars : 0
        }, allSentences);
    }

    private static void ExtractSentences(
        string text, List<ResolvedRubySpan> spans, string sourceHash,
        List<RubySentence> output)
    {
        int spanIdx = 0;
        int sentStart = 0;

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            bool isBoundary = c == '。' || c == '！' || c == '？' || c == '\n';
            if (!isBoundary && i < text.Length - 1) continue;

            var sentEnd = isBoundary ? i + 1 : i + 1;
            var sentence = text[sentStart..sentEnd].Trim();
            sentStart = sentEnd;

            if (sentence.Length < 2) continue;

            // Collect spans whose surface appears in this sentence
            var sentSpans = new List<RubyAnnotation>();
            while (spanIdx < spans.Count)
            {
                var span = spans[spanIdx];
                var pos = sentence.IndexOf(span.Surface, StringComparison.Ordinal);
                if (pos >= 0)
                {
                    sentSpans.Add(new RubyAnnotation { B = span.DictForm, R = span.Reading });
                    spanIdx++;
                }
                else
                {
                    break;
                }
            }

            if (sentSpans.Count > 0)
            {
                output.Add(new RubySentence { S = sentence, R = sentSpans, Src = sourceHash });
            }
        }
    }

    private static (string baseText, string reading, string okurigana) ExtractRubyPair(IElement rubyEl)
    {
        var rtElements = rubyEl.QuerySelectorAll("rt").ToList();
        if (rtElements.Count == 0) return ("", "", "");

        string reading = string.Concat(rtElements.Select(rt => rt.TextContent));

        foreach (var rt in rtElements) rt.Remove();
        foreach (var rp in rubyEl.QuerySelectorAll("rp").ToList()) rp.Remove();

        string baseText;
        var rbElements = rubyEl.QuerySelectorAll("rb").ToList();
        if (rbElements.Count > 0)
            baseText = string.Concat(rbElements.Select(rb => rb.TextContent));
        else
            baseText = rubyEl.TextContent;

        // Capture trailing okurigana from the next sibling text node
        var okurigana = "";
        var nextSibling = rubyEl.NextSibling;
        if (nextSibling is IText textNode)
        {
            var text = textNode.TextContent;
            int okuLen = 0;
            while (okuLen < text.Length && IsHiragana(text[okuLen]))
                okuLen++;
            if (okuLen > 0)
                okurigana = text[..okuLen];
        }

        return (baseText.Trim(), reading.Trim(), okurigana);
    }

    private async Task<List<(string Key, string Content)>?> ReadEpubChapters(string path)
    {
        try
        {
            var readerOptions = new EpubReaderOptions
            {
                SpineReaderOptions = new SpineReaderOptions { IgnoreMissingManifestItems = true }
            };
            var book = await EpubReader.ReadBookAsync(path, readerOptions);
            return FilterChapters(book.ReadingOrder.Select(c => (c.Key, c.Content)));
        }
        catch
        {
            try
            {
                return await ReadEpubManually(path);
            }
            catch
            {
                return null;
            }
        }
    }

    private async Task<List<(string Key, string Content)>?> ReadEpubManually(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var containerEntry = zip.GetEntry("META-INF/container.xml");
        if (containerEntry == null) return null;

        using var containerStream = containerEntry.Open();
        var containerDoc = await XDocument.LoadAsync(containerStream, LoadOptions.None, CancellationToken.None);
        XNamespace cns = "urn:oasis:names:tc:opendocument:xmlns:container";
        var opfPath = containerDoc.Descendants(cns + "rootfile").FirstOrDefault()?.Attribute("full-path")?.Value;
        if (opfPath == null) return null;

        var contentDir = opfPath.Contains('/') ? opfPath[..(opfPath.LastIndexOf('/') + 1)] : "";
        var opfEntry = zip.GetEntry(opfPath);
        if (opfEntry == null) return null;

        using var opfStream = opfEntry.Open();
        var opfDoc = await XDocument.LoadAsync(opfStream, LoadOptions.None, CancellationToken.None);
        XNamespace opfNs = "http://www.idpf.org/2007/opf";

        var manifest = opfDoc.Descendants(opfNs + "item")
            .Where(i => i.Attribute("id") != null && i.Attribute("href") != null && i.Attribute("media-type") != null)
            .ToDictionary(
                i => i.Attribute("id")!.Value,
                i => (Href: i.Attribute("href")!.Value, MediaType: i.Attribute("media-type")!.Value));

        var spineItems = opfDoc.Descendants(opfNs + "itemref")
            .Select(i => i.Attribute("idref")?.Value)
            .Where(id => id != null)
            .ToList();

        var chapters = new List<(string Key, string Content)>();
        foreach (var idref in spineItems)
        {
            if (!manifest.TryGetValue(idref!, out var item)) continue;
            if (!item.MediaType.Contains("html")) continue;

            var entry = zip.GetEntry(contentDir + item.Href);
            if (entry == null) continue;

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            chapters.Add((item.Href, await reader.ReadToEndAsync()));
        }

        return FilterChapters(chapters);
    }

    private static List<(string Key, string Content)> FilterChapters(
        IEnumerable<(string Key, string Content)> chapters)
    {
        return chapters
            .Where(c => !BannedChapterNames.Any(b => c.Key.Contains(b, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string PreprocessHtml(string html)
    {
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return html;
    }

    private void OutputReport(
        ConcurrentBag<BookStats> bookStats,
        ConcurrentDictionary<string, ConcurrentDictionary<string, long>> globalPairs,
        Dictionary<string, HashSet<string>> jmdictMap,
        HashSet<string> nameSet,
        string? outputPath)
    {
        var books = bookStats.OrderByDescending(b => b.RubySpanCount).ToList();
        var booksWithRuby = books.Where(b => b.RubySpanCount > 0).ToList();

        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  RUBY EXTRACTION REPORT");
        Console.WriteLine("═══════════════════════════════════════════════════");

        Console.WriteLine($"\n  Books processed:      {books.Count}");
        Console.WriteLine($"  Books with ruby:      {booksWithRuby.Count} ({100.0 * booksWithRuby.Count / Math.Max(1, books.Count):F1}%)");

        long totalSpansAll = 0;
        foreach (var kv in globalPairs)
            foreach (var rv in kv.Value)
                totalSpansAll += rv.Value;
        Console.WriteLine($"  Total ruby spans:     {totalSpansAll:N0}");
        Console.WriteLine($"  Unique base forms:    {globalPairs.Count:N0}");
        Console.WriteLine($"  Unique (base,reading): {globalPairs.Sum(p => p.Value.Count):N0}");

        // Coverage ratio distribution
        var coverageBuckets = new (string Label, double Min, double Max)[]
        {
            ("0-1%    (selective/rare)", 0, 0.01),
            ("1-5%    (selective)", 0.01, 0.05),
            ("5-15%   (moderate)", 0.05, 0.15),
            ("15-40%  (heavy)", 0.15, 0.40),
            ("40-70%  (near-full)", 0.40, 0.70),
            ("70-100% (full furigana)", 0.70, 1.01)
        };

        Console.WriteLine("\n  Coverage Ratio Distribution (ruby spans / kanji chars):");
        foreach (var (label, min, max) in coverageBuckets)
        {
            var count = booksWithRuby.Count(b => b.CoverageRatio >= min && b.CoverageRatio < max);
            var bar = new string('█', (int)(40.0 * count / Math.Max(1, booksWithRuby.Count)));
            Console.WriteLine($"    {label}: {count,6} {bar}");
        }

        // JMDict matching
        long totalSpansValid = 0;
        long totalSpansCreative = 0;
        long totalSpansNoBase = 0;
        long totalSpansName = 0;
        int pairsValid = 0;
        int pairsCreative = 0;
        int pairsNoBase = 0;
        int pairsName = 0;

        var validPairs = new List<(string Base, string Reading, long Count, string DictReadings)>();
        var creativePairs = new List<(string Base, string Reading, long Count, string DictReadings)>();
        var namePairs = new List<(string Base, string Reading, long Count)>();

        foreach (var (baseText, readings) in globalPairs)
        {
            var hasJmdict = jmdictMap.TryGetValue(baseText, out var dictReadings);
            var isName = nameSet.Contains(baseText);
            var dictStr = hasJmdict ? string.Join("/", dictReadings!) : "";

            foreach (var (reading, count) in readings)
            {
                var isValid = hasJmdict && (dictReadings!.Contains(reading) ||
                    dictReadings.Contains(ApplySmallKana(reading)));

                if (isName)
                {
                    pairsName++;
                    totalSpansName += count;
                    namePairs.Add((baseText, reading, count));
                }
                else if (isValid)
                {
                    pairsValid++;
                    totalSpansValid += count;
                    validPairs.Add((baseText, reading, count, dictStr));
                }
                else if (hasJmdict)
                {
                    pairsCreative++;
                    totalSpansCreative += count;
                    creativePairs.Add((baseText, reading, count, dictStr));
                }
                else
                {
                    pairsNoBase++;
                    totalSpansNoBase += count;
                }
            }
        }

        Console.WriteLine("\n  JMDict Match Analysis:");
        Console.WriteLine($"    Valid readings:            {pairsValid,8:N0} pairs, {totalSpansValid,10:N0} spans");
        Console.WriteLine($"    Names (filtered):          {pairsName,8:N0} pairs, {totalSpansName,10:N0} spans");
        Console.WriteLine($"    Creative (reading not in dict): {pairsCreative,5:N0} pairs, {totalSpansCreative,10:N0} spans");
        Console.WriteLine($"    Base not in dict:          {pairsNoBase,8:N0} pairs, {totalSpansNoBase,10:N0} spans");

        // Ambiguity analysis
        var ambiguousBases = jmdictMap
            .Where(kv => kv.Value.Count >= 2 && !nameSet.Contains(kv.Key))
            .Select(kv => kv.Key).ToHashSet();

        // Group valid pairs by base to find multi-observed-reading bases
        var validByBase = new Dictionary<string, Dictionary<string, long>>();
        foreach (var (b, r, c, _) in validPairs)
        {
            if (!validByBase.TryGetValue(b, out var rs))
            {
                rs = new Dictionary<string, long>();
                validByBase[b] = rs;
            }
            rs[r] = rs.GetValueOrDefault(r) + c;
        }

        var multiObserved = validByBase.Where(kv => kv.Value.Count >= 2).ToDictionary(kv => kv.Key, kv => kv.Value);

        Console.WriteLine("\n  Disambiguation Potential (excluding names):");
        Console.WriteLine($"    JMDict ambiguous forms (2+ readings): {ambiguousBases.Count:N0}");
        Console.WriteLine($"    With any ruby data:                   {validByBase.Keys.Count(k => ambiguousBases.Contains(k)):N0}");
        Console.WriteLine($"    With 2+ OBSERVED readings in ruby:    {multiObserved.Count(kv => ambiguousBases.Contains(kv.Key)):N0}");

        var multiWithSupport = multiObserved
            .Where(kv => ambiguousBases.Contains(kv.Key) && kv.Value.Values.Sum() >= 20)
            .OrderByDescending(kv => kv.Value.Values.Sum())
            .ToList();
        Console.WriteLine($"    With 2+ observed & total >= 20:       {multiWithSupport.Count:N0}");

        // Unambiguous vs ambiguous valid data
        var unambigPairs = validPairs.Where(p => jmdictMap.TryGetValue(p.Base, out var r) && r.Count == 1).ToList();
        var ambigPairs = validPairs.Where(p => jmdictMap.TryGetValue(p.Base, out var r) && r.Count >= 2).ToList();
        Console.WriteLine($"\n    Unambiguous valid pairs (1 dict reading):  {unambigPairs.Count:N0} pairs, {unambigPairs.Sum(p => p.Count):N0} spans");
        Console.WriteLine($"    Ambiguous valid pairs (2+ dict readings):  {ambigPairs.Count:N0} pairs, {ambigPairs.Sum(p => p.Count):N0} spans");

        // Top multi-observed bases
        Console.WriteLine("\n  Top 40 Bases with Multiple Observed Readings:");
        foreach (var (b, rs) in multiWithSupport.Take(40))
        {
            var total = rs.Values.Sum();
            var readingStr = string.Join(", ", rs.OrderByDescending(r => r.Value).Select(r => $"{r.Key}:{r.Value:N0}"));
            Console.WriteLine($"    {b} (n={total:N0}): {readingStr}");
        }

        // Top creative pairs (still remaining after fixes)
        Console.WriteLine("\n  Top 30 Creative Pairs (base in dict, reading not):");
        foreach (var (b, r, c, d) in creativePairs.OrderByDescending(p => p.Count).Take(30))
        {
            Console.WriteLine($"    {b}[{r}] x{c:N0}  (dict: {d})");
        }

        // Top name pairs
        Console.WriteLine("\n  Top 20 Name Pairs (filtered from disambiguation):");
        foreach (var (b, r, c) in namePairs.OrderByDescending(p => p.Count).Take(20))
        {
            Console.WriteLine($"    {b}[{r}] x{c:N0}");
        }

        // Write detailed output
        if (!string.IsNullOrEmpty(outputPath))
        {
            WriteDetailedOutput(outputPath, books, globalPairs, jmdictMap, nameSet, validPairs, creativePairs, multiWithSupport);
            Console.WriteLine($"\nDetailed output written to {outputPath}");
        }
    }

    private void WriteDetailedOutput(
        string outputPath,
        List<BookStats> books,
        ConcurrentDictionary<string, ConcurrentDictionary<string, long>> globalPairs,
        Dictionary<string, HashSet<string>> jmdictMap,
        HashSet<string> nameSet,
        List<(string Base, string Reading, long Count, string DictReadings)> validPairs,
        List<(string Base, string Reading, long Count, string DictReadings)> creativePairs,
        List<KeyValuePair<string, Dictionary<string, long>>> multiObserved)
    {
        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        writer.WriteLine("# Ruby Extraction Detailed Report\n");

        writer.WriteLine("## All Valid Pairs (base\treading\tcount\tdict_readings\tis_ambiguous)");
        foreach (var (b, r, c, d) in validPairs.OrderByDescending(p => p.Count))
        {
            var isAmbig = jmdictMap.TryGetValue(b, out var dr) && dr.Count >= 2;
            writer.WriteLine($"{b}\t{r}\t{c}\t{d}\t{(isAmbig ? "Y" : "N")}");
        }

        writer.WriteLine("\n## Multi-Observed Disambiguation Targets (base\treading_counts\ttotal)");
        foreach (var (b, rs) in multiObserved)
        {
            var total = rs.Values.Sum();
            var readingStr = string.Join("|", rs.OrderByDescending(r => r.Value).Select(r => $"{r.Key}:{r.Value}"));
            var dictStr = jmdictMap.TryGetValue(b, out var dr) ? string.Join("/", dr) : "";
            writer.WriteLine($"{b}\t{readingStr}\t{total}\t{dictStr}");
        }

        writer.WriteLine("\n## Creative Pairs (base\treading\tcount\tdict_readings)");
        foreach (var (b, r, c, d) in creativePairs.OrderByDescending(p => p.Count))
        {
            writer.WriteLine($"{b}\t{r}\t{c}\t{d}");
        }

        writer.WriteLine("\n## Book Stats (path\truby_spans\tkanji_chars\tcoverage_ratio)");
        foreach (var book in books.Where(b => b.RubySpanCount > 0))
        {
            writer.WriteLine($"{book.Path}\t{book.RubySpanCount}\t{book.TotalKanjiChars}\t{book.CoverageRatio:F4}");
        }
    }

    private static bool IsKanji(char c) =>
        (c >= '一' && c <= '鿿') ||
        (c >= '㐀' && c <= '䶿') ||
        (c >= '豈' && c <= '﫿');

    private static bool HasKanji(string s)
    {
        foreach (var c in s)
            if (IsKanji(c)) return true;
        return false;
    }

    private static bool IsHiragana(char c) => c >= 'ぁ' && c <= 'ん';

    private static string ToHiragana(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c >= 'ァ' && c <= 'ヶ')
                sb.Append((char)(c - 0x60));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static (string? dictBase, string? dictReading) TryDeconjugateMatch(
        Dictionary<string, HashSet<string>> jmdictMap,
        string kanjiBase, string rubyReading, string fullInflectedReading)
    {
        var forms = Deconjugator.Instance.Deconjugate(fullInflectedReading);

        foreach (var form in forms.OrderByDescending(f => f.Text.Length))
        {
            var dictReading = form.Text;

            // The dict reading must start with the ruby reading prefix
            if (!dictReading.StartsWith(rubyReading)) continue;

            // Derive the dictionary okurigana
            var dictOku = dictReading[rubyReading.Length..];
            var kanjiDictForm = kanjiBase + dictOku;

            if (jmdictMap.TryGetValue(kanjiDictForm, out var readings) && readings.Contains(dictReading))
                return (kanjiDictForm, dictReading);

            // Also try small-kana normalized version
            var normalized = ApplySmallKana(dictReading);
            if (normalized != dictReading && jmdictMap.TryGetValue(kanjiDictForm, out readings) &&
                readings.Contains(normalized))
                return (kanjiDictForm, normalized);
        }

        return (null, null);
    }

    private static bool TryMatchReading(
        Dictionary<string, HashSet<string>> jmdictMap, string baseForm, string reading,
        out string matchedReading)
    {
        if (jmdictMap.TryGetValue(baseForm, out var dictReadings))
        {
            // Direct match
            if (dictReadings.Contains(reading))
            {
                matchedReading = reading;
                return true;
            }
            // Small-kana fallback: try normalizing the EPUB reading and match against canonical JMDict
            var normalized = ApplySmallKana(reading);
            if (normalized != reading && dictReadings.Contains(normalized))
            {
                matchedReading = normalized;
                return true;
            }
        }
        matchedReading = reading;
        return false;
    }

    private static string ApplySmallKana(string s)
    {
        var sb = new StringBuilder(s);
        for (int i = 0; i < sb.Length - 1; i++)
        {
            var cur = sb[i];
            var next = sb[i + 1];

            if (cur == 'つ' && IsConsonantKana(next))
                sb[i] = 'っ';

            if (IsIColumnKana(cur))
            {
                if (next == 'よ') sb[i + 1] = 'ょ';
                else if (next == 'ゆ') sb[i + 1] = 'ゅ';
                else if (next == 'や') sb[i + 1] = 'ゃ';
            }
        }
        return sb.ToString();
    }

    private static bool IsConsonantKana(char c) =>
        (c >= 'か' && c <= 'こ') || (c >= 'さ' && c <= 'そ') || (c >= 'た' && c <= 'と') ||
        (c >= 'は' && c <= 'ほ') || (c >= 'ぱ' && c <= 'ぽ') ||
        (c >= 'が' && c <= 'ご') || (c >= 'ざ' && c <= 'ぞ') || (c >= 'だ' && c <= 'ど') ||
        (c >= 'ば' && c <= 'ぼ');

    private static bool IsIColumnKana(char c) =>
        c == 'き' || c == 'し' || c == 'ち' || c == 'に' || c == 'ひ' || c == 'み' || c == 'り' ||
        c == 'ぎ' || c == 'じ' || c == 'ぢ' || c == 'び' || c == 'ぴ';

    private static bool IsPlausibleReading(string reading)
    {
        foreach (var c in reading)
        {
            if (c >= 'ぁ' && c <= 'ん') continue;
            if (c >= 'ァ' && c <= 'ヶ') continue;
            if (c == 'ー' || c == '・' || c == 'ゝ' || c == 'ゞ') continue;
            if (c >= 'ㇰ' && c <= 'ㇿ') continue;
            return false;
        }
        return reading.Length > 0;
    }

    private class BookStats
    {
        public string Path { get; init; } = "";
        public int TotalChars { get; init; }
        public int TotalKanjiChars { get; init; }
        public int RubySpanCount { get; init; }
        public double CoverageRatio { get; init; }
    }

    private class ResolvedRubySpan
    {
        public string Surface { get; init; } = "";
        public string DictForm { get; init; } = "";
        public string Reading { get; init; } = "";
    }

    public class RubyAnnotation
    {
        public string B { get; set; } = "";
        public string R { get; set; } = "";
    }

    public class RubySentence
    {
        public string S { get; set; } = "";
        public List<RubyAnnotation> R { get; set; } = [];
        public string Src { get; set; } = "";
    }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(RubyExtractCommands.RubySentence))]
internal partial class SentenceJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
