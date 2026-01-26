using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Jiten.Core.Data;

namespace Jiten.Cli.Commands;

public class BenchmarkCommands(CliContext context)
{
    public async Task RunBenchmark(CliOptions options)
    {
        if (string.IsNullOrEmpty(options.Benchmark))
            return;

        if (!Directory.Exists(options.Benchmark))
        {
            Console.WriteLine($"Directory not found: {options.Benchmark}");
            return;
        }

        var txtFiles = Directory.GetFiles(options.Benchmark, "*.txt")
            .OrderBy(f => f)
            .ToList();

        if (txtFiles.Count == 0)
        {
            Console.WriteLine($"No .txt files found in: {options.Benchmark}");
            return;
        }

        if (!Enum.TryParse(options.DeckType ?? "Novel", out MediaType deckType))
        {
            deckType = MediaType.Novel;
        }

        Console.WriteLine($"Found {txtFiles.Count} txt files");
        Console.WriteLine($"Media type: {deckType}");
        Console.WriteLine($"Warmup: {options.BenchmarkWarmup}");
        Console.WriteLine();

        if (options.BenchmarkWarmup)
        {
            Console.WriteLine("Running warmup parse...");
            var warmupText = "これはテストです。日本語のテキストを解析しています。";
            await Jiten.Parser.Parser.ParseTextToDeck(context.ContextFactory, warmupText, false, false, deckType);
            Console.WriteLine("Warmup complete.");
            Console.WriteLine();
        }

        var results = new List<BenchmarkFileResult>();
        var stopwatch = new Stopwatch();

        Console.WriteLine("=== Benchmark Results ===");
        Console.WriteLine();
        Console.WriteLine("File Results:");

        for (int i = 0; i < txtFiles.Count; i++)
        {
            var filePath = txtFiles[i];
            var fileName = Path.GetFileName(filePath);
            var content = await File.ReadAllTextAsync(filePath);
            var characterCount = content.Length;

            stopwatch.Restart();
            var deck = await Jiten.Parser.Parser.ParseTextToDeck(context.ContextFactory, content, false, false, deckType);
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var wordCount = deck.DeckWords.Count;
            var charsPerSecond = elapsedMs > 0 ? (double)characterCount / elapsedMs * 1000 : 0;

            results.Add(new BenchmarkFileResult
            {
                FileName = fileName,
                CharacterCount = characterCount,
                WordCount = wordCount,
                ElapsedMs = elapsedMs,
                CharsPerSecond = charsPerSecond
            });

            Console.WriteLine($"  {i + 1,3}. {fileName} - {characterCount:N0} chars, {wordCount:N0} words, {elapsedMs:N0} ms ({charsPerSecond:N0} chars/sec)");
        }

        Console.WriteLine();
        Console.WriteLine("Summary:");

        var totalFiles = results.Count;
        var totalCharacters = results.Sum(r => r.CharacterCount);
        var totalWords = results.Sum(r => r.WordCount);
        var totalElapsedMs = results.Sum(r => r.ElapsedMs);
        var averageTimePerFileMs = totalFiles > 0 ? (double)totalElapsedMs / totalFiles : 0;
        var averageCharsPerSecond = totalElapsedMs > 0 ? (double)totalCharacters / totalElapsedMs * 1000 : 0;
        var minTimeMs = results.Count > 0 ? results.Min(r => r.ElapsedMs) : 0;
        var maxTimeMs = results.Count > 0 ? results.Max(r => r.ElapsedMs) : 0;

        Console.WriteLine($"  Files:           {totalFiles}");
        Console.WriteLine($"  Total chars:     {totalCharacters:N0}");
        Console.WriteLine($"  Total words:     {totalWords:N0}");
        Console.WriteLine($"  Total time:      {totalElapsedMs:N0} ms");
        Console.WriteLine($"  Avg time/file:   {averageTimePerFileMs:N1} ms");
        Console.WriteLine($"  Avg chars/sec:   {averageCharsPerSecond:N0}");
        Console.WriteLine($"  Min time:        {minTimeMs:N0} ms");
        Console.WriteLine($"  Max time:        {maxTimeMs:N0} ms");

        if (!string.IsNullOrEmpty(options.Output))
        {
            var benchmarkOutput = new BenchmarkOutput
            {
                Files = results,
                Summary = new BenchmarkSummary
                {
                    TotalFiles = totalFiles,
                    TotalCharacters = totalCharacters,
                    TotalWords = totalWords,
                    TotalElapsedMs = totalElapsedMs,
                    AverageTimePerFileMs = averageTimePerFileMs,
                    AverageCharsPerSecond = averageCharsPerSecond,
                    MinTimeMs = minTimeMs,
                    MaxTimeMs = maxTimeMs
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await File.WriteAllTextAsync(options.Output, JsonSerializer.Serialize(benchmarkOutput, jsonOptions));
            Console.WriteLine();
            Console.WriteLine($"Results written to: {options.Output}");
        }
    }

    private class BenchmarkFileResult
    {
        public string FileName { get; set; } = "";
        public int CharacterCount { get; set; }
        public int WordCount { get; set; }
        public long ElapsedMs { get; set; }
        public double CharsPerSecond { get; set; }
    }

    private class BenchmarkSummary
    {
        public int TotalFiles { get; set; }
        public int TotalCharacters { get; set; }
        public int TotalWords { get; set; }
        public long TotalElapsedMs { get; set; }
        public double AverageTimePerFileMs { get; set; }
        public double AverageCharsPerSecond { get; set; }
        public long MinTimeMs { get; set; }
        public long MaxTimeMs { get; set; }
    }

    private class BenchmarkOutput
    {
        public List<BenchmarkFileResult> Files { get; set; } = [];
        public BenchmarkSummary Summary { get; set; } = new();
    }
}
