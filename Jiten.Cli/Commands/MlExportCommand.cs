using System.Text;
using Jiten.Core;
using Jiten.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Cli.Commands;

public class MlExportCommand(CliContext context)
{
    private static readonly Dictionary<MediaType, string> MediaTypePrefixes = new()
    {
        [MediaType.Anime] = "anime",
        [MediaType.Drama] = "drama",
        [MediaType.Movie] = "movie",
        [MediaType.Novel] = "novel",
        [MediaType.NonFiction] = "nonfiction",
        [MediaType.VideoGame] = "videogame",
        [MediaType.VisualNovel] = "vn",
        [MediaType.WebNovel] = "webnovel",
        [MediaType.Manga] = "manga",
        [MediaType.Audio] = "audio"
    };

    public async Task Export(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var textsDir = Path.Combine(outputDir, "texts");
        Directory.CreateDirectory(textsDir);

        await using var db = await context.ContextFactory.CreateDbContextAsync();

        var allGenres = Enum.GetValues<Genre>().OrderBy(g => (int)g).ToList();

        var allTags = await db.Tags.OrderBy(t => t.Name).ToListAsync();

        Console.WriteLine("Loading decks with genres and tags...");

        var decks = await db.Decks
            .Where(d => d.ParentDeckId == null)
            .Where(d => d.DeckGenres.Any() || d.DeckTags.Any())
            .Include(d => d.DeckGenres)
            .Include(d => d.DeckTags)
            .ThenInclude(dt => dt.Tag)
            .Select(d => new
            {
                d.DeckId,
                d.MediaType,
                d.OriginalTitle,
                Genres = d.DeckGenres.Select(dg => dg.Genre).ToList(),
                Tags = d.DeckTags.Select(dt => new { dt.Tag.Name, dt.Percentage }).ToList()
            })
            .ToListAsync();

        Console.WriteLine($"Found {decks.Count} decks with genres/tags.");

        var tagSet = new HashSet<string>(allTags.Select(t => t.Name));
        var genreColumns = allGenres.Select(g => $"genre:{g}").ToList();
        var tagColumns = allTags.Select(t => $"tag:{t.Name}").ToList();

        var header = new StringBuilder();
        header.Append("work_id");
        foreach (var col in genreColumns) header.Append('\t').Append(col);
        foreach (var col in tagColumns) header.Append('\t').Append(col);

        var labelsPath = Path.Combine(outputDir, "labels.tsv");
        await using var labelsWriter = new StreamWriter(labelsPath, false, Encoding.UTF8);
        await labelsWriter.WriteLineAsync(header.ToString());

        int textsWritten = 0;
        int textsSkipped = 0;

        foreach (var deck in decks)
        {
            var prefix = MediaTypePrefixes.GetValueOrDefault(deck.MediaType, "other");
            var workId = $"{prefix}_{deck.DeckId}";

            var line = new StringBuilder();
            line.Append(workId);

            var deckGenreSet = new HashSet<Genre>(deck.Genres);
            foreach (var genre in allGenres)
                line.Append('\t').Append(deckGenreSet.Contains(genre) ? 1 : 0);

            var deckTagDict = deck.Tags.ToDictionary(t => t.Name, t => (int)t.Percentage);
            foreach (var tag in allTags)
                line.Append('\t').Append(deckTagDict.TryGetValue(tag.Name, out var pct) ? pct : -1);

            await labelsWriter.WriteLineAsync(line.ToString());

            var rawText = await GetDeckText(db, deck.DeckId);
            if (rawText != null)
            {
                var textPath = Path.Combine(textsDir, $"{workId}.txt");
                await File.WriteAllTextAsync(textPath, rawText, Encoding.UTF8);
                textsWritten++;
            }
            else
            {
                textsSkipped++;
            }
        }

        Console.WriteLine($"Export complete: {decks.Count} labels, {textsWritten} text files ({textsSkipped} skipped - no raw text).");
        Console.WriteLine($"Output: {outputDir}");
    }

    private static async Task<string?> GetDeckText(JitenDbContext db, int parentDeckId)
    {
        var children = await db.Decks
            .Where(d => d.ParentDeckId == parentDeckId)
            .OrderBy(d => d.DeckOrder)
            .Select(d => new { d.OriginalTitle, RawText = d.RawText != null ? d.RawText.RawText : null })
            .ToListAsync();

        if (children.Count > 0)
        {
            var parts = children
                .Where(c => c.RawText != null)
                .Select(c => $"───\n{c.OriginalTitle}\n───\n{c.RawText}")
                .ToList();

            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }

        var parentText = await db.DeckRawTexts
            .Where(rt => rt.DeckId == parentDeckId)
            .Select(rt => rt.RawText)
            .FirstOrDefaultAsync();

        return parentText;
    }
}
