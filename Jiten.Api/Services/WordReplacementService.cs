using Hangfire;
using Jiten.Api.Dtos;
using Jiten.Api.Dtos.Requests;
using Jiten.Api.Jobs;
using Jiten.Core;
using Jiten.Core.Data.JMDict;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Services;

public class WordReplacementService(
    IDbContextFactory<JitenDbContext> contextFactory,
    IDbContextFactory<UserDbContext> userContextFactory,
    IBackgroundJobClient backgroundJobs,
    ILogger<WordReplacementService> logger)
{
    public async Task<WordReplacementResult> ReplaceAsync(
        int oldWordId, byte oldReadingIndex,
        int newWordId, byte newReadingIndex,
        bool dryRun = false)
    {
        var result = new WordReplacementResult { WasDryRun = dryRun };

        await using var context = await contextFactory.CreateDbContextAsync();
        await using var userContext = await userContextFactory.CreateDbContextAsync();

        if (dryRun)
        {
            return await ComputeDryRunCounts(context, userContext, oldWordId, oldReadingIndex, newWordId, newReadingIndex);
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        await using var userTransaction = await userContext.Database.BeginTransactionAsync();

        try
        {
            // Collect affected deck IDs before making changes
            var affectedDeckIds = await context.DeckWords
                .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
                .Select(dw => dw.DeckId)
                .Distinct()
                .ToListAsync();

            result.AffectedDeckCount = affectedDeckIds.Count;

            // Step 1: DeckWords - Merge occurrences where both old and new exist
            result.DeckWordsMerged = await context.Database.ExecuteSqlRawAsync(@"
                UPDATE jiten.""DeckWords"" correct
                SET ""Occurrences"" = correct.""Occurrences"" + wrong.""Occurrences""
                FROM jiten.""DeckWords"" wrong
                WHERE correct.""WordId"" = {0}
                  AND correct.""ReadingIndex"" = {1}
                  AND wrong.""WordId"" = {2}
                  AND wrong.""ReadingIndex"" = {3}
                  AND correct.""DeckId"" = wrong.""DeckId""",
                newWordId, newReadingIndex, oldWordId, oldReadingIndex);

            // Step 2: DeckWords - Delete merged old entries
            await context.Database.ExecuteSqlRawAsync(@"
                DELETE FROM jiten.""DeckWords"" wrong
                USING jiten.""DeckWords"" correct
                WHERE wrong.""WordId"" = {0}
                  AND wrong.""ReadingIndex"" = {1}
                  AND correct.""WordId"" = {2}
                  AND correct.""ReadingIndex"" = {3}
                  AND wrong.""DeckId"" = correct.""DeckId""",
                oldWordId, oldReadingIndex, newWordId, newReadingIndex);

            // Step 3: DeckWords - Update remaining (no conflict)
            result.DeckWordsUpdated = await context.Database.ExecuteSqlRawAsync(@"
                UPDATE jiten.""DeckWords""
                SET ""WordId"" = {0}, ""ReadingIndex"" = {1}
                WHERE ""WordId"" = {2} AND ""ReadingIndex"" = {3}",
                newWordId, newReadingIndex, oldWordId, oldReadingIndex);

            // Step 4: ExampleSentenceWords - Delete potential conflicts (extremely rare)
            await context.Database.ExecuteSqlRawAsync(@"
                DELETE FROM jiten.""ExampleSentenceWords"" wrong
                USING jiten.""ExampleSentenceWords"" correct
                WHERE wrong.""WordId"" = {0}
                  AND correct.""WordId"" = {1}
                  AND wrong.""ExampleSentenceId"" = correct.""ExampleSentenceId""
                  AND wrong.""Position"" = correct.""Position""",
                oldWordId, newWordId);

            // Step 5: ExampleSentenceWords - Update remaining
            result.ExampleSentenceWordsUpdated = await context.Database.ExecuteSqlRawAsync(@"
                UPDATE jiten.""ExampleSentenceWords""
                SET ""WordId"" = {0}, ""ReadingIndex"" = {1}
                WHERE ""WordId"" = {2} AND ""ReadingIndex"" = {3}",
                newWordId, newReadingIndex, oldWordId, oldReadingIndex);

            // Step 6: FsrsCards - Only update if user doesn't have the new reading already
            result.FsrsCardsUpdated = await userContext.Database.ExecuteSqlRawAsync(@"
                UPDATE ""user"".""FsrsCards"" old
                SET ""WordId"" = {0}, ""ReadingIndex"" = {1}
                WHERE old.""WordId"" = {2}
                  AND old.""ReadingIndex"" = {3}
                  AND NOT EXISTS (
                    SELECT 1 FROM ""user"".""FsrsCards"" existing
                    WHERE existing.""UserId"" = old.""UserId""
                      AND existing.""WordId"" = {0}
                      AND existing.""ReadingIndex"" = {1}
                  )",
                newWordId, newReadingIndex, oldWordId, oldReadingIndex);

            // Count skipped FsrsCards (users who had both)
            result.FsrsCardsSkipped = await userContext.FsrsCards
                .CountAsync(c => c.WordId == oldWordId && c.ReadingIndex == oldReadingIndex);

            // Step 7: Update UniqueWordCount on affected decks
            if (affectedDeckIds.Count > 0)
            {
                var deckIdsParam = string.Join(",", affectedDeckIds);
                await context.Database.ExecuteSqlRawAsync($@"
                    UPDATE jiten.""Decks"" d
                    SET ""UniqueWordCount"" = (
                        SELECT COUNT(DISTINCT (""WordId"", ""ReadingIndex""))
                        FROM jiten.""DeckWords"" WHERE ""DeckId"" = d.""DeckId""
                    ),
                    ""UniqueWordUsedOnceCount"" = (
                        SELECT COUNT(*)
                        FROM jiten.""DeckWords"" WHERE ""DeckId"" = d.""DeckId"" AND ""Occurrences"" = 1
                    )
                    WHERE d.""DeckId"" IN ({deckIdsParam})");
            }

            await transaction.CommitAsync();
            await userTransaction.CommitAsync();

            // Step 8: Queue parent deck recalculation (outside transaction)
            var parentDeckIds = await context.Decks
                .Where(d => affectedDeckIds.Contains(d.DeckId) && d.ParentDeckId != null)
                .Select(d => d.ParentDeckId!.Value)
                .Distinct()
                .ToListAsync();

            foreach (var parentId in parentDeckIds)
            {
                backgroundJobs.Enqueue<WordReplacementService>(s => s.RecalculateParentDeck(parentId));
            }

            result.ParentDecksQueued = parentDeckIds.Count;

            logger.LogInformation(
                "Word replacement completed: {OldWordId}:{OldReadingIndex} -> {NewWordId}:{NewReadingIndex}. " +
                "DeckWords: {Updated} updated, {Merged} merged. ExampleSentences: {ESUpdated}. " +
                "FsrsCards: {FsrsUpdated} updated, {FsrsSkipped} skipped. Parent decks queued: {Parents}",
                oldWordId, oldReadingIndex, newWordId, newReadingIndex,
                result.DeckWordsUpdated, result.DeckWordsMerged, result.ExampleSentenceWordsUpdated,
                result.FsrsCardsUpdated, result.FsrsCardsSkipped, result.ParentDecksQueued);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            await userTransaction.RollbackAsync();
            logger.LogError(ex, "Word replacement failed: {OldWordId}:{OldReadingIndex} -> {NewWordId}:{NewReadingIndex}",
                oldWordId, oldReadingIndex, newWordId, newReadingIndex);
            throw;
        }

        return result;
    }

    private async Task<WordReplacementResult> ComputeDryRunCounts(
        JitenDbContext context,
        UserDbContext userContext,
        int oldWordId, byte oldReadingIndex,
        int newWordId, byte newReadingIndex)
    {
        var result = new WordReplacementResult { WasDryRun = true };

        // Count DeckWords with old reading
        var oldDeckWordCount = await context.DeckWords
            .CountAsync(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex);

        // Count decks that have BOTH old and new reading (merge case)
        var decksWithOld = context.DeckWords
            .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
            .Select(dw => dw.DeckId);

        var decksWithNew = context.DeckWords
            .Where(dw => dw.WordId == newWordId && dw.ReadingIndex == newReadingIndex)
            .Select(dw => dw.DeckId);

        var mergeCount = await decksWithOld.Intersect(decksWithNew).CountAsync();

        result.DeckWordsMerged = mergeCount;
        result.DeckWordsUpdated = oldDeckWordCount - mergeCount;

        // Count affected decks
        result.AffectedDeckCount = await context.DeckWords
            .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
            .Select(dw => dw.DeckId)
            .Distinct()
            .CountAsync();

        // Count ExampleSentenceWords
        result.ExampleSentenceWordsUpdated = await context.ExampleSentenceWords
            .CountAsync(esw => esw.WordId == oldWordId && esw.ReadingIndex == oldReadingIndex);

        // Count FsrsCards - those that would be updated (user doesn't have new reading)
        var usersWithOld = userContext.FsrsCards
            .Where(c => c.WordId == oldWordId && c.ReadingIndex == oldReadingIndex)
            .Select(c => c.UserId);

        var usersWithNew = userContext.FsrsCards
            .Where(c => c.WordId == newWordId && c.ReadingIndex == newReadingIndex)
            .Select(c => c.UserId);

        var usersWithBoth = await usersWithOld.Intersect(usersWithNew).CountAsync();
        var totalWithOld = await userContext.FsrsCards
            .CountAsync(c => c.WordId == oldWordId && c.ReadingIndex == oldReadingIndex);

        result.FsrsCardsUpdated = totalWithOld - usersWithBoth;
        result.FsrsCardsSkipped = usersWithBoth;

        // Count parent decks that would need recalculation
        var affectedDeckIds = await context.DeckWords
            .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
            .Select(dw => dw.DeckId)
            .Distinct()
            .ToListAsync();

        result.ParentDecksQueued = await context.Decks
            .Where(d => affectedDeckIds.Contains(d.DeckId) && d.ParentDeckId != null)
            .Select(d => d.ParentDeckId!.Value)
            .Distinct()
            .CountAsync();

        return result;
    }

    public async Task RecalculateParentDeck(int parentDeckId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var parentDeck = await context.Decks
            .Include(d => d.Children)
            .ThenInclude(c => c.DeckWords)
            .FirstOrDefaultAsync(d => d.DeckId == parentDeckId);

        if (parentDeck == null)
        {
            logger.LogWarning("Parent deck {DeckId} not found for recalculation", parentDeckId);
            return;
        }

        if (parentDeck.Children.Count == 0)
        {
            logger.LogWarning("Deck {DeckId} has no children to aggregate", parentDeckId);
            return;
        }

        // Delete existing parent DeckWords
        await context.Database.ExecuteSqlRawAsync(
            @"DELETE FROM jiten.""DeckWords"" WHERE ""DeckId"" = {0}",
            parentDeckId);

        // Recalculate using the existing method
        await parentDeck.AddChildDeckWords(context);

        // Bulk insert new DeckWords
        await JitenHelper.BulkInsertDeckWords(contextFactory, parentDeck.DeckWords, parentDeckId);

        // Update deck statistics
        parentDeck.LastUpdate = DateTime.UtcNow;
        await context.SaveChangesAsync();

        logger.LogInformation("Parent deck {DeckId} recalculated with {WordCount} unique words",
            parentDeckId, parentDeck.UniqueWordCount);
    }

    public async Task<SplitWordResult> SplitAsync(
        int oldWordId, byte oldReadingIndex,
        List<WordReadingPair> newWords,
        bool dryRun = false)
    {
        var result = new SplitWordResult { WasDryRun = dryRun };

        if (newWords.Count < 2)
            throw new ArgumentException("Split requires at least 2 new words", nameof(newWords));

        await using var context = await contextFactory.CreateDbContextAsync();

        if (dryRun)
        {
            return await ComputeSplitDryRunCounts(context, oldWordId, oldReadingIndex, newWords);
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // Collect affected deck IDs before making changes
            var affectedDeckIds = await context.DeckWords
                .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
                .Select(dw => dw.DeckId)
                .Distinct()
                .ToListAsync();

            result.AffectedDeckCount = affectedDeckIds.Count;

            // Get old DeckWords with their occurrences (we need these for insertion)
            var oldDeckWords = await context.DeckWords
                .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
                .Select(dw => new { dw.DeckId, dw.Occurrences })
                .ToListAsync();

            result.DeckWordsDeleted = oldDeckWords.Count;

            // For each new word, merge or insert
            foreach (var newWord in newWords)
            {
                // Step 1: Merge - add occurrences to existing entries
                var merged = await context.Database.ExecuteSqlRawAsync(@"
                    UPDATE jiten.""DeckWords"" existing
                    SET ""Occurrences"" = existing.""Occurrences"" + old.""Occurrences""
                    FROM jiten.""DeckWords"" old
                    WHERE existing.""WordId"" = {0}
                      AND existing.""ReadingIndex"" = {1}
                      AND old.""WordId"" = {2}
                      AND old.""ReadingIndex"" = {3}
                      AND existing.""DeckId"" = old.""DeckId""",
                    newWord.WordId, newWord.ReadingIndex, oldWordId, oldReadingIndex);

                result.DeckWordsMerged += merged;

                // Step 2: Insert where new word doesn't exist in deck
                var inserted = await context.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO jiten.""DeckWords"" (""WordId"", ""ReadingIndex"", ""DeckId"", ""Occurrences"")
                    SELECT {0}, {1}, old.""DeckId"", old.""Occurrences""
                    FROM jiten.""DeckWords"" old
                    WHERE old.""WordId"" = {2}
                      AND old.""ReadingIndex"" = {3}
                      AND NOT EXISTS (
                        SELECT 1 FROM jiten.""DeckWords"" existing
                        WHERE existing.""DeckId"" = old.""DeckId""
                          AND existing.""WordId"" = {0}
                          AND existing.""ReadingIndex"" = {1}
                      )",
                    newWord.WordId, newWord.ReadingIndex, oldWordId, oldReadingIndex);

                result.DeckWordsInserted += inserted;
            }

            // Step 3: Delete old entries
            await context.Database.ExecuteSqlRawAsync(@"
                DELETE FROM jiten.""DeckWords""
                WHERE ""WordId"" = {0} AND ""ReadingIndex"" = {1}",
                oldWordId, oldReadingIndex);

            // Handle ExampleSentenceWords - need to calculate positions
            var oldExampleWords = await context.ExampleSentenceWords
                .Where(esw => esw.WordId == oldWordId && esw.ReadingIndex == oldReadingIndex)
                .Select(esw => new { esw.ExampleSentenceId, esw.Position, esw.Length })
                .ToListAsync();

            result.ExampleSentenceWordsDeleted = oldExampleWords.Count;

            if (oldExampleWords.Count > 0)
            {
                // Look up reading lengths for each new word
                var newWordIds = newWords.Select(w => w.WordId).ToList();
                var jmDictWords = await context.JMDictWords
                    .Where(w => newWordIds.Contains(w.WordId))
                    .ToDictionaryAsync(w => w.WordId);

                // Calculate lengths for each new word
                var wordLengths = new List<int>();
                foreach (var newWord in newWords)
                {
                    if (jmDictWords.TryGetValue(newWord.WordId, out var word) &&
                        word.Readings.Count > newWord.ReadingIndex)
                    {
                        wordLengths.Add(word.Readings[newWord.ReadingIndex].Length);
                    }
                    else
                    {
                        wordLengths.Add(1);
                    }
                }

                // Insert new ExampleSentenceWords with calculated positions
                foreach (var oldEsw in oldExampleWords)
                {
                    int cumulativePos = oldEsw.Position;
                    for (int i = 0; i < newWords.Count; i++)
                    {
                        var newWord = newWords[i];
                        var length = wordLengths[i];
                        var bytePos = (byte)cumulativePos;

                        // Check if this entry already exists (conflict)
                        var exists = await context.ExampleSentenceWords
                            .AnyAsync(e => e.ExampleSentenceId == oldEsw.ExampleSentenceId
                                        && e.WordId == newWord.WordId
                                        && e.Position == bytePos);

                        if (!exists)
                        {
                            await context.Database.ExecuteSqlRawAsync(@"
                                INSERT INTO jiten.""ExampleSentenceWords""
                                (""ExampleSentenceId"", ""WordId"", ""Position"", ""Length"", ""ReadingIndex"")
                                VALUES ({0}, {1}, {2}, {3}, {4})",
                                oldEsw.ExampleSentenceId, newWord.WordId, bytePos, length, newWord.ReadingIndex);

                            result.ExampleSentenceWordsInserted++;
                        }

                        cumulativePos += length;
                    }
                }

                // Delete old ExampleSentenceWords
                await context.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM jiten.""ExampleSentenceWords""
                    WHERE ""WordId"" = {0} AND ""ReadingIndex"" = {1}",
                    oldWordId, oldReadingIndex);
            }

            // Update UniqueWordCount on affected decks
            if (affectedDeckIds.Count > 0)
            {
                var deckIdsParam = string.Join(",", affectedDeckIds);
                await context.Database.ExecuteSqlRawAsync($@"
                    UPDATE jiten.""Decks"" d
                    SET ""UniqueWordCount"" = (
                        SELECT COUNT(DISTINCT (""WordId"", ""ReadingIndex""))
                        FROM jiten.""DeckWords"" WHERE ""DeckId"" = d.""DeckId""
                    ),
                    ""UniqueWordUsedOnceCount"" = (
                        SELECT COUNT(*)
                        FROM jiten.""DeckWords"" WHERE ""DeckId"" = d.""DeckId"" AND ""Occurrences"" = 1
                    )
                    WHERE d.""DeckId"" IN ({deckIdsParam})");
            }

            await transaction.CommitAsync();

            // Queue parent deck recalculation (outside transaction)
            var parentDeckIds = await context.Decks
                .Where(d => affectedDeckIds.Contains(d.DeckId) && d.ParentDeckId != null)
                .Select(d => d.ParentDeckId!.Value)
                .Distinct()
                .ToListAsync();

            foreach (var parentId in parentDeckIds)
            {
                backgroundJobs.Enqueue<WordReplacementService>(s => s.RecalculateParentDeck(parentId));
            }

            result.ParentDecksQueued = parentDeckIds.Count;

            logger.LogInformation(
                "Word split completed: {OldWordId}:{OldReadingIndex} -> {NewWordCount} words. " +
                "DeckWords: {Deleted} deleted, {Inserted} inserted, {Merged} merged. " +
                "ExampleSentences: {EsDeleted} deleted, {EsInserted} inserted. Parent decks queued: {Parents}",
                oldWordId, oldReadingIndex, newWords.Count,
                result.DeckWordsDeleted, result.DeckWordsInserted, result.DeckWordsMerged,
                result.ExampleSentenceWordsDeleted, result.ExampleSentenceWordsInserted, result.ParentDecksQueued);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Word split failed: {OldWordId}:{OldReadingIndex}",
                oldWordId, oldReadingIndex);
            throw;
        }

        return result;
    }

    private async Task<SplitWordResult> ComputeSplitDryRunCounts(
        JitenDbContext context,
        int oldWordId, byte oldReadingIndex,
        List<WordReadingPair> newWords)
    {
        var result = new SplitWordResult { WasDryRun = true };

        // Count DeckWords to be deleted
        result.DeckWordsDeleted = await context.DeckWords
            .CountAsync(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex);

        result.AffectedDeckCount = await context.DeckWords
            .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
            .Select(dw => dw.DeckId)
            .Distinct()
            .CountAsync();

        // For each new word, count merges vs inserts
        var decksWithOld = context.DeckWords
            .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
            .Select(dw => dw.DeckId);

        foreach (var newWord in newWords)
        {
            var decksWithNew = context.DeckWords
                .Where(dw => dw.WordId == newWord.WordId && dw.ReadingIndex == newWord.ReadingIndex)
                .Select(dw => dw.DeckId);

            var mergeCount = await decksWithOld.Intersect(decksWithNew).CountAsync();
            result.DeckWordsMerged += mergeCount;
            result.DeckWordsInserted += result.DeckWordsDeleted - mergeCount;
        }

        // Count ExampleSentenceWords
        result.ExampleSentenceWordsDeleted = await context.ExampleSentenceWords
            .CountAsync(esw => esw.WordId == oldWordId && esw.ReadingIndex == oldReadingIndex);

        // For split, each deleted entry produces N new entries (one per new word)
        result.ExampleSentenceWordsInserted = result.ExampleSentenceWordsDeleted * newWords.Count;

        // Count parent decks
        var affectedDeckIds = await context.DeckWords
            .Where(dw => dw.WordId == oldWordId && dw.ReadingIndex == oldReadingIndex)
            .Select(dw => dw.DeckId)
            .Distinct()
            .ToListAsync();

        result.ParentDecksQueued = await context.Decks
            .Where(d => affectedDeckIds.Contains(d.DeckId) && d.ParentDeckId != null)
            .Select(d => d.ParentDeckId!.Value)
            .Distinct()
            .CountAsync();

        return result;
    }

    public async Task<RemoveWordResult> RemoveAsync(
        int wordId, byte readingIndex,
        bool dryRun = false)
    {
        var result = new RemoveWordResult { WasDryRun = dryRun };

        await using var context = await contextFactory.CreateDbContextAsync();
        await using var userContext = await userContextFactory.CreateDbContextAsync();

        if (dryRun)
        {
            return await ComputeRemoveDryRunCounts(context, wordId, readingIndex);
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // Collect affected deck IDs before making changes
            var affectedDeckIds = await context.DeckWords
                .Where(dw => dw.WordId == wordId && dw.ReadingIndex == readingIndex)
                .Select(dw => dw.DeckId)
                .Distinct()
                .ToListAsync();

            result.AffectedDeckCount = affectedDeckIds.Count;

            // Delete DeckWords
            result.DeckWordsDeleted = await context.Database.ExecuteSqlRawAsync(@"
                DELETE FROM jiten.""DeckWords""
                WHERE ""WordId"" = {0} AND ""ReadingIndex"" = {1}",
                wordId, readingIndex);

            // Delete ExampleSentenceWords
            result.ExampleSentenceWordsDeleted = await context.Database.ExecuteSqlRawAsync(@"
                DELETE FROM jiten.""ExampleSentenceWords""
                WHERE ""WordId"" = {0} AND ""ReadingIndex"" = {1}",
                wordId, readingIndex);

            // Update UniqueWordCount on affected decks
            if (affectedDeckIds.Count > 0)
            {
                var deckIdsParam = string.Join(",", affectedDeckIds);
                await context.Database.ExecuteSqlRawAsync($@"
                    UPDATE jiten.""Decks"" d
                    SET ""UniqueWordCount"" = (
                        SELECT COUNT(DISTINCT (""WordId"", ""ReadingIndex""))
                        FROM jiten.""DeckWords"" WHERE ""DeckId"" = d.""DeckId""
                    ),
                    ""UniqueWordUsedOnceCount"" = (
                        SELECT COUNT(*)
                        FROM jiten.""DeckWords"" WHERE ""DeckId"" = d.""DeckId"" AND ""Occurrences"" = 1
                    )
                    WHERE d.""DeckId"" IN ({deckIdsParam})");
            }

            await transaction.CommitAsync();

            // Queue parent deck recalculation (outside transaction)
            var parentDeckIds = await context.Decks
                .Where(d => affectedDeckIds.Contains(d.DeckId) && d.ParentDeckId != null)
                .Select(d => d.ParentDeckId!.Value)
                .Distinct()
                .ToListAsync();

            foreach (var parentId in parentDeckIds)
            {
                backgroundJobs.Enqueue<WordReplacementService>(s => s.RecalculateParentDeck(parentId));
            }

            result.ParentDecksQueued = parentDeckIds.Count;

            logger.LogInformation(
                "Word removal completed: {WordId}:{ReadingIndex}. " +
                "DeckWords: {DwDeleted}. ExampleSentences: {EsDeleted}." +
                "Affected decks: {Decks}. Parent decks queued: {Parents}",
                wordId, readingIndex,
                result.DeckWordsDeleted, result.ExampleSentenceWordsDeleted,
                result.AffectedDeckCount, result.ParentDecksQueued);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Word removal failed: {WordId}:{ReadingIndex}",
                wordId, readingIndex);
            throw;
        }

        return result;
    }

    private async Task<RemoveWordResult> ComputeRemoveDryRunCounts(
        JitenDbContext context,
        int wordId, byte readingIndex)
    {
        var result = new RemoveWordResult { WasDryRun = true };

        result.DeckWordsDeleted = await context.DeckWords
            .CountAsync(dw => dw.WordId == wordId && dw.ReadingIndex == readingIndex);

        result.AffectedDeckCount = await context.DeckWords
            .Where(dw => dw.WordId == wordId && dw.ReadingIndex == readingIndex)
            .Select(dw => dw.DeckId)
            .Distinct()
            .CountAsync();

        result.ExampleSentenceWordsDeleted = await context.ExampleSentenceWords
            .CountAsync(esw => esw.WordId == wordId && esw.ReadingIndex == readingIndex);

        var affectedDeckIds = await context.DeckWords
            .Where(dw => dw.WordId == wordId && dw.ReadingIndex == readingIndex)
            .Select(dw => dw.DeckId)
            .Distinct()
            .ToListAsync();

        result.ParentDecksQueued = await context.Decks
            .Where(d => affectedDeckIds.Contains(d.DeckId) && d.ParentDeckId != null)
            .Select(d => d.ParentDeckId!.Value)
            .Distinct()
            .CountAsync();

        return result;
    }
}
