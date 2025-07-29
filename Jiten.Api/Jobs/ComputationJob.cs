using System.Text;
using CsvHelper;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Jiten.Api.Jobs;

public class ComputationJob(JitenDbContext context, IConfiguration configuration)
{
    public async Task RecomputeFrequencies()
    {
        string path = Path.Join(configuration["StaticFilesPath"], "yomitan");
        Directory.CreateDirectory(path);

        Console.WriteLine("Computing global frequencies...");
        var frequencies = await JitenHelper.ComputeFrequencies(context.DbOptions, null);
        await JitenHelper.SaveFrequenciesToDatabase(context.DbOptions, frequencies);

        // Save frequencies to CSV
        await SaveFrequenciesToCsv(frequencies, Path.Join(path, "jiten_freq_global.csv"));

        // Generate Yomitan deck
        var bytes = await YomitanHelper.GenerateYomitanFrequencyDeck(context.DbOptions, frequencies, null);
        var filePath = Path.Join(path, "jiten_freq_global.zip");
        await File.WriteAllBytesAsync(filePath, bytes);

        foreach (var mediaType in Enum.GetValues<MediaType>())
        {
            Console.WriteLine($"Computing {mediaType} frequencies...");
            frequencies = await JitenHelper.ComputeFrequencies(context.DbOptions, mediaType);

            // Save frequencies to CSV
            await SaveFrequenciesToCsv(frequencies, Path.Join(path, $"jiten_freq_{mediaType.ToString()}.csv"));

            // Generate Yomitan deck
            bytes = await YomitanHelper.GenerateYomitanFrequencyDeck(context.DbOptions, frequencies, mediaType);
            filePath = Path.Join(path, $"jiten_freq_{mediaType.ToString()}.zip");
            await File.WriteAllBytesAsync(filePath, bytes);
        }
    }

    private async Task SaveFrequenciesToCsv(List<JmDictWordFrequency> frequencies, string filePath)
    {
        // Fetch words from the database
        Dictionary<int, JmDictWord> allWords = await context.JMDictWords.AsNoTracking()
                                                            .Where(w => frequencies.Select(f => f.WordId).Contains(w.WordId))
                                                            .ToDictionaryAsync(w => w.WordId);

        List<(string word, int rank)> frequencyList = new();

        foreach (var frequency in frequencies)
        {
            if (!allWords.TryGetValue(frequency.WordId, out var word)) continue;

            var highestPercentage = frequency.ReadingsFrequencyPercentage.Max();
            var index = frequency.ReadingsFrequencyPercentage.IndexOf(highestPercentage);
            string readingWord = word.Readings[index];

            frequencyList.Add((readingWord, frequency.FrequencyRank));
        }

        // Create CSV file
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Create anonymous object for CsvWriter
        var frequencyListCsv = frequencyList.Select(f => new { Word = f.word, Rank = f.rank }).ToArray();

        await csv.WriteRecordsAsync(frequencyListCsv);
        await writer.FlushAsync();

        stream.Position = 0;
        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await stream.CopyToAsync(fileStream);
    }
}