using Jiten.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Jiten.Parser;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // var text = "「あそこ美味しいよねー。早くお祭り終わって欲しいなー。ノンビリ遊びに行きたーい」";
        var text = await File.ReadAllTextAsync("Y:\\00_JapaneseStudy\\JL\\Backlogs\\Default_2024.12.28_10.52.47-2024.12.28_19.58.40.txt");

        var configuration = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("sharedsettings.json", optional: true, reloadOnChange: true)
                            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                            .AddEnvironmentVariables()
                            .Build();

        var connectionString = configuration.GetConnectionString("JitenDatabase");
        var optionsBuilder = new DbContextOptionsBuilder<JitenDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => { o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery); });

        await using var context = new JitenDbContext(optionsBuilder.Options);

        await Parser.ParseTextToDeck(context, text, predictDifficulty:false);

        // Console.InputEncoding = Encoding.UTF8;
        // Console.OutputEncoding = Encoding.UTF8;
        //
        // while (true)
        // {
        //     var text = Console.ReadLine();
        //
        //     if (string.IsNullOrWhiteSpace(text))
        //         return;
        //
        //     var deck = await ParseTextToDeck(context, text);
        //     Console.WriteLine(JsonSerializer.Serialize(deck));
        //     Console.WriteLine();
        // }
    }
}