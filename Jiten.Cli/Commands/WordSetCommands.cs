using Jiten.Core;
using Jiten.Core.Data.JMDict;
using Jiten.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Cli.Commands;

public class WordSetCommands(CliContext context)
{
    public async Task CreateWordSetFromPartOfSpeech(string slug, string name, string? description, string pos, bool syncKana)
    {
        var posValues = pos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Console.WriteLine($"Creating WordSet '{name}' from PoS containing '{string.Join("', '", posValues)}'...");

        await using var jitenContext = new JitenDbContext(context.DbOptions);

        if (await jitenContext.WordSets.AnyAsync(ws => ws.Slug == slug))
        {
            Console.WriteLine($"WordSet with slug '{slug}' already exists.");
            return;
        }

        var seenWordIds = new HashSet<int>();

        foreach (var p in posValues)
        {
            var words = await jitenContext.JMDictWords
                .AsNoTracking()
                .Where(w => w.PartsOfSpeech.Any(ps => ps.Contains(p)))
                .Select(w => w.WordId)
                .ToListAsync();

            int added = 0;
            foreach (var wordId in words)
            {
                if (seenWordIds.Add(wordId))
                    added++;
            }
            Console.WriteLine($"PoS '{p}': {words.Count} words found, {added} new (deduplicated).");
        }

        Console.WriteLine($"Total: {seenWordIds.Count} unique words.");

        var wordSet = new WordSet
        {
            Slug = slug,
            Name = name,
            Description = description,
            WordCount = 0
        };
        await jitenContext.WordSets.AddAsync(wordSet);
        await jitenContext.SaveChangesAsync();

        var allForms = await jitenContext.WordForms
            .AsNoTracking()
            .Where(wf => seenWordIds.Contains(wf.WordId))
            .OrderBy(wf => wf.WordId)
            .ThenBy(wf => wf.ReadingIndex)
            .ToListAsync();

        var formsByWord = allForms.GroupBy(f => f.WordId).ToDictionary(g => g.Key, g => g.ToList());

        var wordReadings = new List<(int WordId, short ReadingIndex)>();
        int kanjiReadingsCount = 0;
        int kanaOnlyReadingsCount = 0;
        foreach (var wordId in seenWordIds)
        {
            if (!formsByWord.TryGetValue(wordId, out var forms)) continue;
            bool hasKanjiForm = forms.Any(f => f.FormType == JmDictFormType.KanjiForm);
            foreach (var form in forms)
            {
                if (form.FormType == JmDictFormType.KanjiForm)
                {
                    wordReadings.Add((wordId, form.ReadingIndex));
                    kanjiReadingsCount++;
                }
                else if (form.FormType == JmDictFormType.KanaForm && !hasKanjiForm)
                {
                    wordReadings.Add((wordId, form.ReadingIndex));
                    kanaOnlyReadingsCount++;
                }
            }
        }
        Console.WriteLine($"Selected {kanjiReadingsCount} kanji readings, {kanaOnlyReadingsCount} kana-only readings (total: {wordReadings.Count}).");

        await AddWordSetMembersWithKanaSync(jitenContext, wordSet.SetId, wordReadings, syncKana);

        var memberStats = await jitenContext.WordSetMembers
            .Where(m => m.SetId == wordSet.SetId)
            .GroupBy(_ => 1)
            .Select(g => new { WordCount = g.Select(m => m.WordId).Distinct().Count(), FormCount = g.Count() })
            .FirstOrDefaultAsync();

        wordSet.WordCount = memberStats?.WordCount ?? 0;
        await jitenContext.SaveChangesAsync();

        Console.WriteLine($"Created WordSet '{name}' with {wordSet.WordCount} words ({memberStats?.FormCount ?? 0} forms).");
    }

    public async Task CreateWordSetFromCsv(string slug, string name, string? description, string csvFile, bool syncKana)
    {
        Console.WriteLine($"Creating WordSet '{name}' from CSV file...");

        await using var jitenContext = new JitenDbContext(context.DbOptions);

        if (await jitenContext.WordSets.AnyAsync(ws => ws.Slug == slug))
        {
            Console.WriteLine($"WordSet with slug '{slug}' already exists.");
            return;
        }

        var lines = await File.ReadAllLinesAsync(csvFile);
        var wordReadings = new List<(int WordId, short ReadingIndex)>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 2) continue;

            if (!int.TryParse(parts[0].Trim(), out var wordId)) continue;
            if (!short.TryParse(parts[1].Trim(), out var readingIndex)) continue;
            if (readingIndex < 0 || readingIndex > byte.MaxValue) continue;

            wordReadings.Add((wordId, readingIndex));
        }

        Console.WriteLine($"Parsed {wordReadings.Count} word-reading pairs from CSV.");

        var wordIds = wordReadings.Select(w => w.WordId).Distinct().ToList();
        var existingWordIds = await jitenContext.JMDictWords
            .AsNoTracking()
            .Where(w => wordIds.Contains(w.WordId))
            .Select(w => w.WordId)
            .ToHashSetAsync();

        var validReadings = wordReadings.Where(w => existingWordIds.Contains(w.WordId)).ToList();
        var invalidCount = wordReadings.Count - validReadings.Count;
        if (invalidCount > 0)
        {
            Console.WriteLine($"Skipped {invalidCount} entries with invalid WordIds.");
        }

        var wordSet = new WordSet
        {
            Slug = slug,
            Name = name,
            Description = description,
            WordCount = 0
        };
        await jitenContext.WordSets.AddAsync(wordSet);
        await jitenContext.SaveChangesAsync();

        await AddWordSetMembersWithKanaSync(jitenContext, wordSet.SetId, validReadings, syncKana);

        var memberStats = await jitenContext.WordSetMembers
            .Where(m => m.SetId == wordSet.SetId)
            .GroupBy(_ => 1)
            .Select(g => new { WordCount = g.Select(m => m.WordId).Distinct().Count(), FormCount = g.Count() })
            .FirstOrDefaultAsync();

        wordSet.WordCount = memberStats?.WordCount ?? 0;
        await jitenContext.SaveChangesAsync();

        Console.WriteLine($"Created WordSet '{name}' with {wordSet.WordCount} words ({memberStats?.FormCount ?? 0} forms).");
    }

    private static async Task AddWordSetMembersWithKanaSync(
        JitenDbContext jitenContext,
        int setId,
        IEnumerable<(int WordId, short ReadingIndex)> words,
        bool syncKana)
    {
        var wordsList = words.ToList();
        if (wordsList.Count == 0) return;

        var wordIds = wordsList.Select(w => w.WordId).Distinct().ToList();

        Dictionary<int, List<JmDictWordForm>>? formsByWord = null;
        if (syncKana)
        {
            var allForms = await jitenContext.WordForms
                .AsNoTracking()
                .Where(wf => wordIds.Contains(wf.WordId))
                .OrderBy(wf => wf.WordId)
                .ThenBy(wf => wf.ReadingIndex)
                .ToListAsync();
            formsByWord = allForms.GroupBy(f => f.WordId).ToDictionary(g => g.Key, g => g.ToList());
        }

        var processedKeys = new HashSet<(int WordId, short ReadingIndex)>();
        var members = new List<WordSetMember>();
        int position = 0;

        int kanaAdded = 0;
        int skippedNoTypes = 0;
        int skippedNotReading = 0;
        int skippedNoKana = 0;

        foreach (var (wordId, readingIndex) in wordsList)
        {
            if (processedKeys.Add((wordId, readingIndex)))
            {
                members.Add(new WordSetMember
                {
                    SetId = setId,
                    WordId = wordId,
                    ReadingIndex = readingIndex,
                    Position = position++
                });
            }

            if (!syncKana || formsByWord == null) continue;
            if (!formsByWord.TryGetValue(wordId, out var wordForms))
            {
                skippedNoTypes++;
                continue;
            }
            var currentForm = wordForms.FirstOrDefault(wf => wf.ReadingIndex == readingIndex);
            if (currentForm == null) continue;
            if (currentForm.FormType != JmDictFormType.KanjiForm)
            {
                skippedNotReading++;
                continue;
            }

            var kanaForm = wordForms.FirstOrDefault(wf => wf.FormType == JmDictFormType.KanaForm);
            if (kanaForm == null)
            {
                skippedNoKana++;
                continue;
            }

            if (processedKeys.Add((wordId, kanaForm.ReadingIndex)))
            {
                members.Add(new WordSetMember
                {
                    SetId = setId,
                    WordId = wordId,
                    ReadingIndex = kanaForm.ReadingIndex,
                    Position = position++
                });
                kanaAdded++;
            }
        }

        if (syncKana)
        {
            Console.WriteLine($"Kana sync: {kanaAdded} kana forms added, {skippedNoTypes} missing types, {skippedNotReading} not Reading type, {skippedNoKana} no KanaReading found.");
        }

        const int batchSize = 10000;
        for (int i = 0; i < members.Count; i += batchSize)
        {
            var batch = members.Skip(i).Take(batchSize).ToList();
            await jitenContext.WordSetMembers.AddRangeAsync(batch);
            await jitenContext.SaveChangesAsync();
            Console.WriteLine($"Inserted {Math.Min(i + batchSize, members.Count)}/{members.Count} members...");
        }
    }
}
