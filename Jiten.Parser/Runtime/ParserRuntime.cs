using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jiten.Core;
using Jiten.Core.Data.JMDict;
using Jiten.Parser.Data.Redis;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Parser.Runtime;

internal sealed class ParserRuntime
{
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _initialized;
    private ParserRuntimeSnapshot _snapshot = null!;

    public async Task<ParserRuntimeSnapshot> EnsureInitializedAsync(
        IDbContextFactory<JitenDbContext> contextFactory, Action<string>? log = null)
    {
        if (_initialized)
            return _snapshot;

        await _initSemaphore.WaitAsync();
        try
        {
            if (!_initialized)
            {
                _snapshot = await InitializeAsync(contextFactory, log);
                _initialized = true;
            }

            return _snapshot;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private static async Task<ParserRuntimeSnapshot> InitializeAsync(
        IDbContextFactory<JitenDbContext> contextFactory, Action<string>? log = null)
    {
        var runtimeSettings = ParserRuntimeSettings.Current;

        IDeckWordCache deckWordCache = new RedisDeckWordCache(runtimeSettings.Configuration);
        IJmDictCache jmDictCache = new RedisJmDictCache(runtimeSettings.Configuration, contextFactory);

        var overallSw = Stopwatch.StartNew();

        // Sudachi context creation and Deconjugator JSON load are independent of the DB —
        // run them concurrently with the three preload queries so they're free on the critical path.
        var sudachiSw = Stopwatch.StartNew();
        var sudachiWarmupTask = Task.Run(static async () =>
        {
            _ = Deconjugator.Instance;
            await new MorphologicalAnalyser().Parse("食べた");
        });

        var (lookups, wordFrequencyRanks, nameOnlyWordIds, expressionWordIds, lookupsMs, freqMs, nameOnlyMs) =
            await LoadPreloadDataAsync(contextFactory);
        var dbWallMs = overallSw.ElapsedMilliseconds;

        await sudachiWarmupTask;
        sudachiSw.Stop();

        log?.Invoke(
            $"Warmup phases — " +
            $"lookups: {lookupsMs}ms, freqRanks: {freqMs}ms, nameOnlyIds: {nameOnlyMs}ms " +
            $"(DB wall: {dbWallMs}ms) | sudachi: {sudachiSw.ElapsedMilliseconds}ms " +
            $"(waited {Math.Max(0, sudachiSw.ElapsedMilliseconds - dbWallMs)}ms after DB)");

        // Redis prefill runs in the background — GetWordsAsync has a DB fallback so parsing
        // works correctly even while the cache is still being populated on a cold start.
        _ = Task.Run(() => PrefillRedisCacheAsync(jmDictCache, contextFactory));

        return new ParserRuntimeSnapshot(deckWordCache, jmDictCache, lookups, wordFrequencyRanks, nameOnlyWordIds, expressionWordIds);
    }

    private static async Task<(Dictionary<string, List<int>> lookups, Dictionary<int, int> wordFrequencyRanks,
        HashSet<int> nameOnlyWordIds, HashSet<int> expressionWordIds, long lookupsMs, long freqMs, long nameOnlyMs)>
        LoadPreloadDataAsync(IDbContextFactory<JitenDbContext> contextFactory)
    {
        await using var ctx1 = await contextFactory.CreateDbContextAsync();
        await using var ctx2 = await contextFactory.CreateDbContextAsync();
        await using var ctx3 = await contextFactory.CreateDbContextAsync();
        await using var ctx4 = await contextFactory.CreateDbContextAsync();

        // ContinueWith captures elapsed time at the moment each individual task completes,
        // giving the per-task duration even though all three run concurrently.
        long lookupsMs = 0, freqMs = 0, nameOnlyMs = 0;
        var sw = Stopwatch.StartNew();

        var t1 = JmDictHelper.LoadLookupTable(ctx1)
            .ContinueWith(t => { lookupsMs = sw.ElapsedMilliseconds; return t.Result; },
                TaskContinuationOptions.ExecuteSynchronously);
        var t2 = JmDictHelper.LoadWordFrequencyRanks(ctx2)
            .ContinueWith(t => { freqMs = sw.ElapsedMilliseconds; return t.Result; },
                TaskContinuationOptions.ExecuteSynchronously);
        var t3 = JmDictHelper.LoadNameOnlyWordIds(ctx3)
            .ContinueWith(t => { nameOnlyMs = sw.ElapsedMilliseconds; return t.Result; },
                TaskContinuationOptions.ExecuteSynchronously);
        var t4 = JmDictHelper.LoadExpressionWordIds(ctx4);

        await Task.WhenAll(t1, t2, t3, t4);

        return (t1.Result, t2.Result, t3.Result, t4.Result, lookupsMs, freqMs, nameOnlyMs);
    }

    private static async Task PrefillRedisCacheAsync(IJmDictCache jmDictCache, IDbContextFactory<JitenDbContext> contextFactory)
    {
        try
        {
            if (await jmDictCache.IsCacheInitializedAsync())
                return;

            await using var ctx = await contextFactory.CreateDbContextAsync();

            // Pre-compute the archaic flag from a small targeted query (only arch-tagged words + their def POS).
            // This avoids loading all 215K definitions into memory just to strip them immediately after.
            var fullyArchaicIds = await JmDictHelper.LoadFullyArchaicWordIds(ctx);

            // Stream words in small batches WITHOUT definitions (~2-3GB savings vs. LoadAllWords).
            // ComputeArchaicFlag in SetWordsAsync respects IsFullyArchaic when Definitions is empty.
            await JmDictHelper.StreamWordBatchesAsync(ctx, 2000, async batch =>
            {
                foreach (var word in batch)
                    word.IsFullyArchaic = fullyArchaicIds.Contains(word.WordId);

                await jmDictCache.SetWordsAsync(batch.ToDictionary(w => w.WordId, w => w));
            });

            await jmDictCache.SetCacheInitializedAsync();
        }
        catch
        {
            // Non-fatal: GetWordsAsync falls back to the database on cache misses.
        }
    }
}

internal sealed class ParserRuntimeSnapshot(
    IDeckWordCache deckWordCache,
    IJmDictCache jmDictCache,
    Dictionary<string, List<int>> lookups,
    Dictionary<int, int> wordFrequencyRanks,
    HashSet<int> nameOnlyWordIds,
    HashSet<int> expressionWordIds)
{
    public IDeckWordCache DeckWordCache { get; } = deckWordCache;
    public IJmDictCache JmDictCache { get; } = jmDictCache;
    public Dictionary<string, List<int>> Lookups { get; } = lookups;
    public Dictionary<int, int> WordFrequencyRanks { get; } = wordFrequencyRanks;
    public HashSet<int> NameOnlyWordIds { get; } = nameOnlyWordIds;
    public HashSet<int> ExpressionWordIds { get; } = expressionWordIds;
}
