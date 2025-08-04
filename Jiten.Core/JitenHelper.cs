using System.Diagnostics;
using BunnyCDN.Net.Storage;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jiten.Core;

public static class JitenHelper
{
    public static async Task InsertDeck(DbContextOptions<JitenDbContext> options, Deck deck, byte[] cover, bool update = false)
    {
        await using var context = new JitenDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var existingDeck =
                await context.Decks
                             .Include(d => d.DeckWords)
                             .Include(d => d.Children).ThenInclude(d => d.DeckWords).OrderBy(d => d.DeckOrder)
                             .Include(d => d.RawText)
                             .Include(d => d.ExampleSentences)
                             .FirstOrDefaultAsync(d => d.OriginalTitle == deck.OriginalTitle && d.MediaType == deck.MediaType);

            if (existingDeck != null)
            {
                if (!update || update && existingDeck.LastUpdate >= deck.LastUpdate)
                {
                    Console.WriteLine($"Deck {deck.OriginalTitle} already exists, no update flag or deck not changed, skipping.");
                    await transaction.RollbackAsync();
                    return;
                }
                else
                {
                    Console.WriteLine($"Deck {deck.OriginalTitle} already exists, updating.");
                    await UpdateDeck(context, existingDeck, deck);
                }
            }
            else
            {
                // Fix potential null references to decks
                deck.SetParentsAndDeckWordDeck(deck);
                deck.ParentDeckId = null;

                // Prevent efcore from inserting the deckwords as we will do it with an optimized method later
                var deckWordsToInsert = deck.DeckWords.ToList();
                deck.DeckWords = new List<DeckWord>();

                // Do the same for example sentences to prevent EF Core from inserting them prematurely
                var exampleSentencesToInsert = deck.ExampleSentences?.ToList() ?? new List<ExampleSentence>();
                if (deck.ExampleSentences != null)
                    deck.ExampleSentences = new List<ExampleSentence>();

                context.Decks.Add(deck);

                await context.SaveChangesAsync();

                using var coverOptimized = new ImageMagick.MagickImage(cover);

                coverOptimized.Resize(400, 400);
                coverOptimized.Strip();
                coverOptimized.Quality = 85;
                coverOptimized.Format = ImageMagick.MagickFormat.Jpeg;

                var coverUrl = await BunnyCdnHelper.UploadFile(coverOptimized.ToByteArray(), $"{deck.DeckId}/cover.jpg");
                deck.CoverName = coverUrl;

                await BulkInsertDeckWords(context, deckWordsToInsert, deck.DeckId);

                if (deck.ExampleSentences != null && exampleSentencesToInsert.Any())
                {
                    await BulkInsertExampleSentences(context, exampleSentencesToInsert, deck.DeckId);
                }

                await InsertChildDecks(context, deck.Children, deck.DeckId);

                context.Entry(deck).State = EntityState.Modified;
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Error inserting deck: {ex.Message}");
        }
    }

    private static async Task BulkInsertDeckWords(JitenDbContext context, ICollection<DeckWord> deckWords, int deckId)
    {
        if (!deckWords.Any()) return;

        const int batchSize = 5000;
        var batches = deckWords.Chunk(batchSize);

        foreach (var batch in batches)
        {
            var sql = $@"
                        INSERT INTO jiten.""DeckWords"" (""WordId"", ""ReadingIndex"", ""Occurrences"", ""DeckId"") VALUES " +
                      "";
            var parameters = new List<object>();
            var values = new List<string>();

            for (int i = 0; i < batch.Length; i++)
            {
                var dw = batch[i];
                values.Add($"({{{parameters.Count}}}, {{{parameters.Count + 1}}}, {{{parameters.Count + 2}}}, {{{parameters.Count + 3}}})");
                parameters.AddRange([dw.WordId, dw.ReadingIndex, dw.Occurrences, deckId]);
            }

            sql += string.Join(", ", values);
            await context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());
        }
    }

    private static async Task BulkInsertExampleSentences(JitenDbContext context, ICollection<ExampleSentence> exampleSentences, int deckId)
    {
        if (!exampleSentences.Any()) return;

        const int batchSize = 1000;
        var batches = exampleSentences.Chunk(batchSize);
        var sentenceIdMapping = new Dictionary<ExampleSentence, int>();

        // First, insert all example sentences and keep track of their generated IDs
        foreach (var batch in batches)
        {
            var sql = $@"
                        INSERT INTO jiten.""ExampleSentences"" (""Text"", ""Position"", ""DeckId"") 
                        VALUES {string.Join(", ", Enumerable.Range(0, batch.Length).Select(i => $"({{{i * 3}}}, {{{i * 3 + 1}}}, {{{i * 3 + 2}}})"))}
                        RETURNING ""SentenceId""";

            var parameters = new List<object>();

            foreach (var sentence in batch)
            {
                parameters.Add(sentence.Text);
                parameters.Add(sentence.Position);
                parameters.Add(deckId);
            }

            var sentenceIds = await context.Database.SqlQueryRaw<int>(sql, parameters.ToArray()).ToListAsync();

            // Store the mapping between sentences and their IDs
            for (int i = 0; i < batch.Length; i++)
            {
                sentenceIdMapping[batch[i]] = sentenceIds[i];
                // Update the entity with the generated ID
                batch[i].SentenceId = sentenceIds[i];
                batch[i].DeckId = deckId;
            }
        }

        // Then, insert all example sentence words using the generated sentence IDs
        var allWords = new List<ExampleSentenceWord>();

        foreach (var sentence in exampleSentences)
        {
            foreach (var word in sentence.Words)
            {
                word.ExampleSentenceId = sentence.SentenceId;
                allWords.Add(word);
            }
        }

        if (!allWords.Any()) return;

        var wordBatches = allWords.Chunk(batchSize);

        foreach (var batch in wordBatches)
        {
            var sql = $@"
                        INSERT INTO jiten.""ExampleSentenceWords"" (""ExampleSentenceId"", ""WordId"", ""ReadingIndex"", ""Position"", ""Length"") VALUES " +
                      "";
            var parameters = new List<object>();
            var values = new List<string>();

            for (int i = 0; i < batch.Length; i++)
            {
                var word = batch[i];
                values.Add($"({{{parameters.Count}}}, {{{parameters.Count + 1}}}, {{{parameters.Count + 2}}}, {{{parameters.Count + 3}}}, {{{parameters.Count + 4}}})");
                parameters.AddRange([word.ExampleSentenceId, word.WordId, word.ReadingIndex, word.Position, word.Length]);
            }

            sql += string.Join(", ", values);
            await context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());
        }
    }

    private static async Task UpdateDeck(JitenDbContext context, Deck existingDeck, Deck deck)
    {
        var deckId = existingDeck.DeckId;

        existingDeck.LastUpdate = DateTime.UtcNow;
        existingDeck.MediaType = deck.MediaType;
        existingDeck.OriginalTitle = deck.OriginalTitle;
        existingDeck.RomajiTitle = deck.RomajiTitle;
        existingDeck.EnglishTitle = deck.EnglishTitle;
        existingDeck.CharacterCount = deck.CharacterCount;
        existingDeck.WordCount = deck.WordCount;
        existingDeck.UniqueWordCount = deck.UniqueWordCount;
        existingDeck.UniqueWordUsedOnceCount = deck.UniqueWordUsedOnceCount;
        existingDeck.UniqueKanjiCount = deck.UniqueKanjiCount;
        existingDeck.UniqueKanjiUsedOnceCount = deck.UniqueKanjiUsedOnceCount;
        existingDeck.SentenceCount = deck.SentenceCount;
        existingDeck.Difficulty = deck.Difficulty;
        existingDeck.DialoguePercentage = deck.DialoguePercentage;
        existingDeck.RawText = deck.RawText;

        await context.Database.ExecuteSqlRawAsync($@"DELETE FROM jiten.""DeckWords"" WHERE ""DeckId"" = {{0}}", deckId);

        // Delete existing example sentences (cascade will delete their words too)
        await context.Database.ExecuteSqlRawAsync($@"DELETE FROM jiten.""ExampleSentences"" WHERE ""DeckId"" = {{0}}", deckId);

        await BulkInsertDeckWords(context, deck.DeckWords, deckId);

        if (deck.ExampleSentences != null && deck.ExampleSentences.Any())
        {
            await BulkInsertExampleSentences(context, deck.ExampleSentences, deckId);
        }

        await UpdateChildDecks(context, existingDeck, deck.Children);
    }

    private static async Task InsertChildDecks(JitenDbContext context, ICollection<Deck> children, int parentDeckId)
    {
        if (!children.Any()) return;

        // Store DeckWords separately for each child to prevent EF from inserting them
        var childDeckWordsToInsert = new Dictionary<Deck, List<DeckWord>>();

        foreach (var child in children)
        {
            child.ParentDeckId = parentDeckId;
            child.CreationDate = DateTime.UtcNow;
            child.LastUpdate = DateTimeOffset.UtcNow;

            // Prevent EF Core from inserting the DeckWords - we'll do it with bulk insert
            childDeckWordsToInsert[child] = child.DeckWords.ToList();
            child.DeckWords = new List<DeckWord>();

            context.Entry(child).State = child.DeckId == 0 ? EntityState.Added : EntityState.Modified;

        }

        await context.SaveChangesAsync();

        foreach (var child in children)
        {
            await BulkInsertDeckWords(context, childDeckWordsToInsert[child], child.DeckId);
        }
    }

    private static async Task UpdateChildDecks(JitenDbContext context, Deck existingDeck, ICollection<Deck> children)
    {
        var existingChildren = await context.Decks
                                            .Where(d => d.ParentDeckId == existingDeck.DeckId)
                                            .ToListAsync();

        var newChildren = children.ToDictionary(c => c.OriginalTitle);
        var existingChildrenDict = existingChildren.ToDictionary(c => c.OriginalTitle);

        foreach (var child in children)
        {
            var key = child.OriginalTitle;

            if (existingChildrenDict.TryGetValue(key, out var existingChild))
            {
                Console.WriteLine("Updating child deck " + key);

                try
                {
                    await context.Entry(existingChild)
                                 .Reference(nameof(existingChild.RawText))
                                 .LoadAsync();
                }
                catch (Exception loadEx)
                {
                    Console.WriteLine($"Error explicitly loading data for child deck {key}: {loadEx.Message}");
                    continue;
                }

                await UpdateDeck(context, existingChild, child);
            }
            else
            {
                Console.WriteLine("Inserting new child deck " + key);

                var newChildDeck = new Deck
                                   {
                                       ParentDeckId = existingDeck.DeckId, ParentDeck = existingDeck, CreationDate = DateTime.UtcNow,
                                       LastUpdate = DateTimeOffset.UtcNow, MediaType = existingDeck.MediaType,
                                       OriginalTitle = child.OriginalTitle, RomajiTitle = child.RomajiTitle,
                                       EnglishTitle = child.EnglishTitle, DeckOrder = child.DeckOrder,
                                       CharacterCount = child.CharacterCount, UniqueWordCount = child.UniqueWordCount,
                                       UniqueKanjiCount = child.UniqueKanjiCount, SentenceCount = child.SentenceCount,
                                       WordCount = child.WordCount, Difficulty = child.Difficulty,
                                       DialoguePercentage = child.DialoguePercentage, RawText = child.RawText
                                   };

                context.Decks.Add(newChildDeck);
                await context.SaveChangesAsync(); // Save to get the ID

                await BulkInsertDeckWords(context, child.DeckWords, newChildDeck.DeckId);
            }
        }

        // Bulk delete removed children
        var childrenToDelete = existingChildren
                               .Where(ec => children.All(c => c.OriginalTitle != ec.OriginalTitle))
                               .Select(c => c.DeckId)
                               .ToList();

        if (childrenToDelete.Any())
        {
            // Delete DeckWords first (foreign key constraint)
            await context.Database.ExecuteSqlRawAsync(
                                                      $@"DELETE FROM jiten.""DeckWords"" WHERE ""DeckId"" IN ({string.Join(",", childrenToDelete.Select((_, i) => $"{{{i}}}"))})",
                                                      childrenToDelete.Cast<object>().ToArray());

            // Delete the decks
            await context.Database.ExecuteSqlRawAsync(
                                                      $@"DELETE FROM jiten.""Decks"" WHERE ""DeckId"" IN ({string.Join(",", childrenToDelete.Select((_, i) => $"{{{i}}}"))})",
                                                      childrenToDelete.Cast<object>().ToArray());

            Console.WriteLine($"Deleted {childrenToDelete.Count} child decks.");
        }
    }

    /// <summary>
    /// Computes frequency data for a specific media type, or globally if no type is specified.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="mediaType">The media type to compute frequencies for. If null, calculates global frequencies.</param>
    /// <returns>A list of JmDictWordFrequency objects, sorted by rank.</returns>
    public static async Task<List<JmDictWordFrequency>> ComputeFrequencies(DbContextOptions<JitenDbContext> options, MediaType? mediaType = null)
    {
        await using var context = new JitenDbContext(options);
        
        Dictionary<int, JmDictWordFrequency> wordFrequencies = new();
        var wordReadingCounts = await context.JMDictWords
                                             .AsNoTracking()
                                             .Select(w => new { w.WordId, ReadingsCount = w.Readings.Count })
                                             .ToDictionaryAsync(w => w.WordId, w => w.ReadingsCount);

        foreach (var kvp in wordReadingCounts)
        {
            wordFrequencies.Add(kvp.Key, new JmDictWordFrequency
                                         {
                                             WordId = kvp.Key, FrequencyRank = 0, UsedInMediaAmount = 0,
                                             ReadingsFrequencyPercentage = [.. new double[kvp.Value]],
                                             ReadingsObservedFrequency = [.. new double[kvp.Value]],
                                             ReadingsFrequencyRank = [.. new int[kvp.Value]],
                                             ReadingsUsedInMediaAmount = [.. new int[kvp.Value]],
                                         });
        }

        var primaryDecksQuery = context.Decks.AsNoTracking().Where(d => d.ParentDeck == null);

        if (mediaType.HasValue)
        {
            primaryDecksQuery = primaryDecksQuery.Where(d => d.MediaType == mediaType.Value);
        }

        var primaryDecks = await primaryDecksQuery.Select(d => d.DeckId).ToListAsync();

        if (!primaryDecks.Any())
        {
            // Return empty or default frequencies if no decks match
            return wordFrequencies.Values.ToList();
        }

        var wordAggregates = await context.DeckWords
                                          .Where(d => primaryDecks.Contains(d.DeckId))
                                          .GroupBy(d => d.WordId)
                                          .Select(g => new
                                                       {
                                                           WordId = g.Key, TotalOccurrences = g.Sum(dw => dw.Occurrences),
                                                           DistinctDeckCount =
                                                               g.Select(dw => dw.DeckId).Distinct().Count()
                                                       })
                                          .ToListAsync();

        foreach (var agg in wordAggregates)
        {
            if (wordFrequencies.TryGetValue(agg.WordId, out var freq))
            {
                freq.FrequencyRank = agg.TotalOccurrences; // Store raw count
                freq.UsedInMediaAmount = agg.DistinctDeckCount;
            }
        }

        var readingAggregates = await context.DeckWords
                                             .Where(d => primaryDecks.Contains(d.DeckId))
                                             .GroupBy(d => new { d.WordId, d.ReadingIndex })
                                             .Select(g => new
                                                          {
                                                              g.Key.WordId, g.Key.ReadingIndex,
                                                              TotalOccurrences = g.Sum(dw => dw.Occurrences), EntryCount = g.Count()
                                                          })
                                             .ToListAsync();

        foreach (var agg in readingAggregates)
        {
            if (wordFrequencies.TryGetValue(agg.WordId, out var freq))
            {
                freq.ReadingsFrequencyRank[agg.ReadingIndex] = agg.TotalOccurrences; // Store raw count
                freq.ReadingsUsedInMediaAmount[agg.ReadingIndex] = agg.EntryCount;
            }
        }

        // Apply logarithmic scaling + document frequency formula
        foreach (var word in wordFrequencies.Values)
        {
            word.ObservedFrequency = word.FrequencyRank;
            double score = Math.Log(1 + word.FrequencyRank) * word.UsedInMediaAmount;
            word.FrequencyRank = (int)Math.Round(score * 100);

            for (int j = 0; j < word.ReadingsFrequencyRank.Count; j++)
            {
                word.ReadingsObservedFrequency[j] = word.ReadingsFrequencyRank[j];
                double readingScore = Math.Log(1 + word.ReadingsFrequencyRank[j]) * word.ReadingsUsedInMediaAmount[j];
                word.ReadingsFrequencyRank[j] = (int)Math.Round(readingScore * 100);
            }
        }

        var sortedWordFrequencies = wordFrequencies.Values
                                                   .OrderByDescending(w => w.FrequencyRank)
                                                   .ToList();

        long totalOccurrences = sortedWordFrequencies.Sum(w => (long)w.ObservedFrequency);

        int currentRank = 0;
        int previousScore = -1;
        int rankStep = 0;

        for (int i = 0; i < sortedWordFrequencies.Count; i++)
        {
            var word = sortedWordFrequencies[i];

            int wordRawFrequencyCount = (int)word.ObservedFrequency;
            int wordScore = word.FrequencyRank;

            for (int j = 0; j < word.ReadingsObservedFrequency.Count; j++)
            {
                double readingRawCount = word.ReadingsObservedFrequency[j];

                if (totalOccurrences > 0)
                    word.ReadingsObservedFrequency[j] = readingRawCount / (double)totalOccurrences;
                else
                    word.ReadingsObservedFrequency[j] = 0;

                if (wordRawFrequencyCount > 0)
                    word.ReadingsFrequencyPercentage[j] = (readingRawCount / (double)wordRawFrequencyCount) * 100.0;
                else
                    word.ReadingsFrequencyPercentage[j] = 0;
            }

            if (totalOccurrences > 0)
                word.ObservedFrequency = wordRawFrequencyCount / (double)totalOccurrences;
            else
                word.ObservedFrequency = 0;

            if (wordScore != previousScore)
            {
                currentRank += rankStep;
                rankStep = 1;
                previousScore = wordScore;
            }
            else
            {
                rankStep++;
            }

            word.FrequencyRank = currentRank + 1;
        }

        var readingFrequencyCounts = new Dictionary<int, int>();
        foreach (var wordFreq in sortedWordFrequencies)
        {
            foreach (int readingScore in wordFreq.ReadingsFrequencyRank)
            {
                if (readingScore <= 0) continue;
                readingFrequencyCounts.TryGetValue(readingScore, out int currentCount);
                readingFrequencyCounts[readingScore] = currentCount + 1;
            }
        }

        var sortedReadingFrequencies = readingFrequencyCounts.OrderByDescending(kvp => kvp.Key).ToList();
        var readingFrequencyFinalRanks = new Dictionary<int, int>();
        currentRank = 1;
        foreach (var kvp in sortedReadingFrequencies)
        {
            readingFrequencyFinalRanks.Add(kvp.Key, currentRank);
            currentRank += kvp.Value;
        }

        int zeroReadingRank = currentRank;

        foreach (var wordFreq in sortedWordFrequencies)
        {
            for (int i = 0; i < wordFreq.ReadingsFrequencyRank.Count; i++)
            {
                int readingScore = wordFreq.ReadingsFrequencyRank[i];
                if (readingScore > 0 && readingFrequencyFinalRanks.TryGetValue(readingScore, out int finalRank))
                {
                    wordFreq.ReadingsFrequencyRank[i] = finalRank;
                }
                else
                {
                    wordFreq.ReadingsFrequencyRank[i] = zeroReadingRank;
                }
            }
        }

        return sortedWordFrequencies;
    }

    public static async Task SaveFrequenciesToDatabase(DbContextOptions<JitenDbContext> options, List<JmDictWordFrequency> frequencies)
    {
        await using var context = new JitenDbContext(options);

        Console.WriteLine("Updating database with frequencies...");

        // Delete previous frequency data if it exists
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE jmdict.\"WordFrequencies\"");

        // Bulk insert using PostgreSQL COPY command
        const string tempTable = "temp_word_frequencies";
        await using var conn = new NpgsqlConnection(context.Database.GetConnectionString());
        await conn.OpenAsync();

        await using (var cmd = new NpgsqlCommand($@"CREATE TEMP TABLE {tempTable} (LIKE jmdict.""WordFrequencies"" INCLUDING ALL)", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var writer =
                     await
                         conn.BeginBinaryImportAsync($@"COPY {tempTable} (""WordId"", ""FrequencyRank"", ""ObservedFrequency"", ""UsedInMediaAmount"", ""ReadingsFrequencyPercentage"", ""ReadingsObservedFrequency"", ""ReadingsFrequencyRank"", ""ReadingsUsedInMediaAmount"") FROM STDIN (FORMAT BINARY)"))
        {
            foreach (var word in frequencies)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(word.WordId);
                await writer.WriteAsync(word.FrequencyRank);
                await writer.WriteAsync(word.ObservedFrequency);
                await writer.WriteAsync(word.UsedInMediaAmount);
                await writer.WriteAsync(word.ReadingsFrequencyPercentage, NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Double);
                await writer.WriteAsync(word.ReadingsObservedFrequency, NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Double);
                await writer.WriteAsync(word.ReadingsFrequencyRank, NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer);
                await writer.WriteAsync(word.ReadingsUsedInMediaAmount, NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer);
            }

            await writer.CompleteAsync();
        }

        await using (var cmd = new NpgsqlCommand($@"
        INSERT INTO jmdict.""WordFrequencies"" (""WordId"", ""FrequencyRank"", ""ObservedFrequency"", ""UsedInMediaAmount"", ""ReadingsFrequencyPercentage"", ""ReadingsObservedFrequency"", ""ReadingsFrequencyRank"", ""ReadingsUsedInMediaAmount"")
        SELECT ""WordId"", ""FrequencyRank"", ""ObservedFrequency"", ""UsedInMediaAmount"", ""ReadingsFrequencyPercentage"", ""ReadingsObservedFrequency"", ""ReadingsFrequencyRank"", ""ReadingsUsedInMediaAmount"" FROM {tempTable};
        DROP TABLE {tempTable};", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine("Database update complete.");
    }

    /// <summary>
    /// Get infos about a deck to debug the difficulty
    /// </summary>
    public static async Task DebugDeck(DbContextOptions<JitenDbContext> options, int deckId)
    {
        await using var context = new JitenDbContext(options);

        // Retrieve the deck
        var deck = await context.Decks
                                .AsNoTracking()
                                .FirstOrDefaultAsync(d => d.DeckId == deckId);
        if (deck == null)
        {
            Console.WriteLine($"Deck with id {deckId} not found.");
            return;
        }

        Console.WriteLine("=============");
        Console.WriteLine("Deck info");
        Console.WriteLine("=============");
        Console.WriteLine("Original name: " + deck.OriginalTitle);
        Console.WriteLine("Romaji name: " + deck.RomajiTitle);
        Console.WriteLine("Character count: " + deck.CharacterCount);


        var deckWords = await context.DeckWords
                                     .AsNoTracking()
                                     .Where(dw => dw.DeckId == deckId)
                                     .ToListAsync();

        var wordIds = deckWords.Select(dw => dw.WordId)
                               .Distinct()
                               .ToList();

        var wordFrequencies = await context.JmDictWordFrequencies
                                           .AsNoTracking()
                                           .Where(wf => wordIds.Contains(wf.WordId))
                                           .ToListAsync();

        var words = await context.JMDictWords
                                 .AsNoTracking()
                                 .Where(w => wordIds.Contains(w.WordId))
                                 .ToListAsync();

        var query = new List<(JmDictWord word, JmDictWordFrequency frequency)>();

        foreach (var wf in wordFrequencies)
        {
            foreach (var w in words)
            {
                if (wf.WordId == w.WordId)
                {
                    query.Add(new(w, wf));
                }
            }
        }

        // var rarestWords = query.OrderByDescending(x => x.frequency.FrequencyRank)
        //                        .Take(100)
        //                        .ToList();
        //
        // Console.WriteLine("First 100 rarest words in deck:");
        // foreach (var item in rarestWords)
        // {
        //     Console.WriteLine(item.word.Readings[0] + " - " + item.frequency.FrequencyRank);
        // }


        Dictionary<int, int> wordFrequencyBy10k = new Dictionary<int, int>();
        foreach (var w in words)
        {
            var frequencyRank = wordFrequencies.First(wf => wf.WordId == w.WordId).FrequencyRank;
            var nRank = frequencyRank / 1000;

            if (wordFrequencyBy10k.ContainsKey(nRank))
                wordFrequencyBy10k[nRank]++;
            else
                wordFrequencyBy10k[nRank] = 1;
        }

        var list = wordFrequencyBy10k.Select(x => (x.Key, x.Value))
                                     .OrderBy(x => x.Key)
                                     .ToList();

        double totalPoints = 0;
        foreach (var wf in list)
        {
            var points = (wf.Value / (double)words.Count * 100) * Math.Exp(wf.Key / 10.0);
            totalPoints += points;
        }

        foreach (var wf in list)
        {
            var points = (wf.Value / (double)words.Count * 100) * Math.Exp(wf.Key / 10.0);

            Console.WriteLine($"Percentage of words in rank {wf.Key} : {(wf.Value / (double)words.Count * 100f).ToString("F2")}%");
            Console.WriteLine("Points: " + points + " - Percentage of total points: " + (points / totalPoints * 100f).ToString("F2") + "%");
        }

        Console.WriteLine("Total points: " + totalPoints);
    }
}