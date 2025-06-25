using System.Text.Json;
using Jiten.Core;
using Jiten.Core.Data.JMDict;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Jiten.Parser.Data.Redis;

public class RedisJmDictCache : IJmDictCache
{
    private readonly IDatabase _redisDb;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromDays(30); // Long cache time for dictionary data
    private const string InitializedKey = "jmdict:initialized";
    private readonly JitenDbContext _dbContext;

    public RedisJmDictCache(IConfiguration configuration, JitenDbContext dbContext)
    {
        var connection = ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!);
        _redisDb = connection.GetDatabase();
        _dbContext = dbContext;
    }

    private string BuildLookupKey(string lookupText)
    {
        return $"jmdict:lookup:{lookupText}";
    }

    private string BuildWordKey(int wordId)
    {
        return $"jmdict:word:{wordId}";
    }

    public async Task<List<int>> GetLookupIdsAsync(string key)
    {
        var redisKey = BuildLookupKey(key);
        var json = await _redisDb.StringGetAsync(redisKey);
        if (json.IsNullOrEmpty)
        {
            await using var dbContext = new JitenDbContext(_dbContext.DbOptions);
            // Fetch the lookup from database
            var lookupIds = await dbContext.Lookups
                                           .AsNoTracking()
                                           .Where(l => l.LookupKey == key)
                                           .Select(l => l.WordId)
                                           .ToListAsync();

            // Cache the result if found
            if (lookupIds.Any())
            {
                var newJson = JsonSerializer.Serialize(lookupIds, _jsonOptions);
                await _redisDb.StringSetAsync(redisKey, newJson, expiry: _cacheExpiry);
            }

            return lookupIds;
        }

        return JsonSerializer.Deserialize<List<int>>(json!, _jsonOptions) ?? new List<int>();
    }

    public async Task<Dictionary<string, List<int>>> GetLookupIdsAsync(IEnumerable<string> keys)
    {
        var uniqueKeys = keys.Distinct().ToList();
        if (!uniqueKeys.Any())
        {
            return new Dictionary<string, List<int>>();
        }

        var redisKeys = uniqueKeys.Select(k => (RedisKey)BuildLookupKey(k)).ToArray();

        // 1. Fetch all keys from Redis in a single MGET command
        var redisValues = await _redisDb.StringGetAsync(redisKeys);

        var results = new Dictionary<string, List<int>>();
        var missedKeys = new List<string>();

        // 2. Process the results from Redis
        for (int i = 0; i < redisKeys.Length; i++)
        {
            var key = uniqueKeys[i];
            var value = redisValues[i];

            if (value.IsNullOrEmpty)
            {
                missedKeys.Add(key);
            }
            else
            {
                results[key] = JsonSerializer.Deserialize<List<int>>(value!, _jsonOptions) ?? new List<int>();
            }
        }

        // 3. If any keys were not in the cache, fetch them from the database in a single query
        if (missedKeys.Any())
        {
            await using var dbContext = new JitenDbContext(_dbContext.DbOptions);

            var dbLookups = await dbContext.Lookups
                                           .AsNoTracking()
                                           .Where(l => missedKeys.Contains(l.LookupKey))
                                           .Select(l => new { l.LookupKey, l.WordId })
                                           .ToListAsync();

            var dbResults = dbLookups
                            .GroupBy(l => l.LookupKey)
                            .ToDictionary(g => g.Key, g => g.Select(l => l.WordId).ToList());

            // 4. Add the database results to our main results and prepare to cache them
            var cacheBatch = _redisDb.CreateBatch();
            foreach (var kvp in dbResults)
            {
                results[kvp.Key] = kvp.Value;
                var redisKey = BuildLookupKey(kvp.Key);
                var json = JsonSerializer.Serialize(kvp.Value, _jsonOptions);
                // Don't await here, just add to the batch
                cacheBatch.StringSetAsync(redisKey, json, expiry: _cacheExpiry);
            }

            // Execute the batch to write all new entries to Redis
            cacheBatch.Execute();
        }

        return results;
    }

    public async Task<JmDictWord?> GetWordAsync(int wordId)
    {
        var redisKey = BuildWordKey(wordId);
        var json = await _redisDb.StringGetAsync(redisKey);
        if (json.IsNullOrEmpty)
        {
            using var dbContext = new JitenDbContext(_dbContext.DbOptions);

            // Fetch the word from database
            var word = await dbContext.JMDictWords
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(w => w.WordId == wordId);

            // Cache the result if found
            if (word != null)
            {
                var newJson = JsonSerializer.Serialize(word, _jsonOptions);
                await _redisDb.StringSetAsync(redisKey, newJson, expiry: _cacheExpiry);
            }

            return word;
        }

        return JsonSerializer.Deserialize<JmDictWord>(json!, _jsonOptions);
    }

    public async Task<bool> SetLookupIdsAsync(Dictionary<string, List<int>> lookups)
    {
        var batch = _redisDb.CreateBatch();
        var tasks = new List<Task<bool>>();

        foreach (var lookup in lookups)
        {
            var redisKey = BuildLookupKey(lookup.Key);
            var json = JsonSerializer.Serialize(lookup.Value, _jsonOptions);
            tasks.Add(batch.StringSetAsync(redisKey, json, expiry: _cacheExpiry));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        return tasks.All(t => t.Result);
    }

    public async Task<bool> SetWordAsync(int wordId, JmDictWord word)
    {
        var redisKey = BuildWordKey(wordId);
        var json = JsonSerializer.Serialize(word, _jsonOptions);
        return await _redisDb.StringSetAsync(redisKey, json, expiry: _cacheExpiry);
    }

    public async Task<bool> SetWordsAsync(Dictionary<int, JmDictWord> words)
    {
        var batch = _redisDb.CreateBatch();
        var tasks = new List<Task<bool>>();

        foreach (var (wordId, word) in words)
        {
            var redisKey = BuildWordKey(wordId);
            var json = JsonSerializer.Serialize(word, _jsonOptions);
            tasks.Add(batch.StringSetAsync(redisKey, json, expiry: _cacheExpiry));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        return tasks.All(t => t.Result);
    }

    public async Task<bool> IsCacheInitializedAsync()
    {
        return await _redisDb.KeyExistsAsync(InitializedKey);
    }

    public async Task SetCacheInitializedAsync()
    {
        await _redisDb.StringSetAsync(InitializedKey, "1", expiry: _cacheExpiry);
    }
}