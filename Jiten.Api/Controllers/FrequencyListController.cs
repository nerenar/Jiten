using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper;
using Jiten.Core;
using Jiten.Core.Data.JMDict;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/frequency-list")]
public class FrequencyListController(JitenDbContext context) : ControllerBase
{
    [HttpGet("get-global-frequency-list")]
    [ResponseCache(Duration = 60 * 60 * 24)]
    [EnableRateLimiting("download")]
    public async Task<IResult> GetGlobalFrequencyList()
    {
        var frequencies = await context.JmDictWordFrequencies.AsNoTracking().OrderBy(w => w.FrequencyRank).ToListAsync();
        Dictionary<int, JmDictWord> allWords = await context.JMDictWords.AsNoTracking()
                                                            .Where(w => frequencies.Select(f => f.WordId).Contains(w.WordId))
                                                            .ToDictionaryAsync(w => w.WordId);
        List<(string word, int rank)> frequencyList = new();

        foreach (var frequency in frequencies)
        {
            var highestPercentage = frequency.ReadingsFrequencyPercentage.Max();

            string word = "";
            var index = frequency.ReadingsFrequencyPercentage.IndexOf(highestPercentage);
            word = allWords[frequency.WordId].Readings[index];


            frequencyList.Add((word, frequency.FrequencyRank));
        }

        var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Create anonymous object since CsvWriter doesn't support writing tuples
        object[] frequencyListCsv = frequencyList.Select(f => new { Word = f.word, Rank = f.rank }).ToArray<object>();

        await csv.WriteRecordsAsync(frequencyListCsv);
        var bytes = stream.ToArray();

        return Results.File(bytes, "text/csv", "frequency_list.csv");
    }
}