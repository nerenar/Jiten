using System.Collections.Generic;
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

    public async Task<ParserRuntimeSnapshot> EnsureInitializedAsync(IDbContextFactory<JitenDbContext> contextFactory)
    {
        if (_initialized)
            return _snapshot;

        await _initSemaphore.WaitAsync();
        try
        {
            if (!_initialized)
            {
                _snapshot = await InitializeAsync(contextFactory);
                _initialized = true;
            }

            return _snapshot;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private static async Task<ParserRuntimeSnapshot> InitializeAsync(IDbContextFactory<JitenDbContext> contextFactory)
    {
        var runtimeSettings = ParserRuntimeSettings.Current;

        await using var context = await contextFactory.CreateDbContextAsync();

        IDeckWordCache deckWordCache = new RedisDeckWordCache(runtimeSettings.Configuration);
        IJmDictCache jmDictCache = new RedisJmDictCache(runtimeSettings.Configuration, contextFactory);

        var lookups = await JmDictHelper.LoadLookupTable(context);
        var nameOnlyWordIds = await JmDictHelper.LoadNameOnlyWordIds(context);

        if (!await jmDictCache.IsCacheInitializedAsync())
        {
            var allWords = await JmDictHelper.LoadAllWords(context);
            const int batchSize = 10000;

            for (int i = 0; i < allWords.Count; i += batchSize)
            {
                var wordsBatch = allWords.Skip(i).Take(batchSize)
                                         .ToDictionary(w => w.WordId, w => w);
                await jmDictCache.SetWordsAsync(wordsBatch);
            }

            await jmDictCache.SetCacheInitializedAsync();
        }

        return new ParserRuntimeSnapshot(deckWordCache, jmDictCache, lookups, nameOnlyWordIds);
    }
}

internal sealed class ParserRuntimeSnapshot(
    IDeckWordCache deckWordCache,
    IJmDictCache jmDictCache,
    Dictionary<string, List<int>> lookups,
    HashSet<int> nameOnlyWordIds)
{
    public IDeckWordCache DeckWordCache { get; } = deckWordCache;
    public IJmDictCache JmDictCache { get; } = jmDictCache;
    public Dictionary<string, List<int>> Lookups { get; } = lookups;
    public HashSet<int> NameOnlyWordIds { get; } = nameOnlyWordIds;
}
