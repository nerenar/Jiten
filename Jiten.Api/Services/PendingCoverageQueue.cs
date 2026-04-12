using StackExchange.Redis;

namespace Jiten.Api.Services;

public interface IPendingCoverageQueue
{
    Task AddAsync(int deckId);
    Task AddManyAsync(IEnumerable<int> deckIds);
    Task<List<int>> DrainAsync();

    // Increments a bounded retry counter for a user whose batch coverage job was contended.
    // Returns true if the caller may re-queue the deckIds for another attempt, false if the
    // retry budget is exhausted and the caller should fall back (e.g., mark CoverageDirty).
    Task<bool> TryRecordContentionAsync(string userId, int maxAttempts);
}

public class PendingCoverageQueue(IConnectionMultiplexer redis, ILogger<PendingCoverageQueue> logger) : IPendingCoverageQueue
{
    private const string Key = "coverage:pending_decks";
    private static readonly TimeSpan ContentionTtl = TimeSpan.FromHours(2);

    public async Task AddAsync(int deckId)
    {
        try
        {
            await redis.GetDatabase().SetAddAsync(Key, deckId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add deck {DeckId} to pending coverage queue", deckId);
        }
    }

    public async Task AddManyAsync(IEnumerable<int> deckIds)
    {
        var values = deckIds.Select(id => (RedisValue)id).ToArray();
        if (values.Length == 0) return;
        try
        {
            await redis.GetDatabase().SetAddAsync(Key, values);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add {Count} decks to pending coverage queue", values.Length);
        }
    }

    // Atomically reads and clears the pending set. Decks added concurrently mid-drain
    // remain in the set for the next sweep (MULTI/EXEC executes server-side without interleaving).
    public async Task<List<int>> DrainAsync()
    {
        try
        {
            var db = redis.GetDatabase();
            var tx = db.CreateTransaction();
            var membersTask = tx.SetMembersAsync(Key);
            _ = tx.KeyDeleteAsync(Key);
            var ok = await tx.ExecuteAsync();
            if (!ok) return [];

            var members = await membersTask;
            var result = new List<int>(members.Length);
            foreach (var m in members)
            {
                if (int.TryParse(m, out var id))
                    result.Add(id);
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to drain pending coverage queue");
            return [];
        }
    }

    public async Task<bool> TryRecordContentionAsync(string userId, int maxAttempts)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"coverage:batch-contention:{userId}";
            var count = await db.StringIncrementAsync(key);
            if (count == 1)
                await db.KeyExpireAsync(key, ContentionTtl);
            return count <= maxAttempts;
        }
        catch (Exception ex)
        {
            // On Redis failure fall through to the safe path (caller should mark dirty).
            logger.LogWarning(ex, "Failed to record batch contention for {UserId}", userId);
            return false;
        }
    }
}
