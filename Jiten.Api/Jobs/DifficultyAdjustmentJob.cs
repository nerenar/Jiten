using Hangfire;
using Jiten.Core;
using Jiten.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Jobs;

public class DifficultyAdjustmentJob(
    IDbContextFactory<JitenDbContext> contextFactory,
    IDbContextFactory<UserDbContext> userContextFactory,
    ILogger<DifficultyAdjustmentJob> logger)
{
    private const decimal LOGISTIC_SCALE_S = 0.9m;
    private const decimal LEARNING_RATE = 0.05m;
    private const decimal REGULARIZATION = 0.10m;
    private const int ITERATIONS = 15;
    private const decimal ABSOLUTE_WEIGHT = 0.20m;
    private const decimal MAX_ADJUSTMENT_BASE = 1.0m;
    private const decimal CAP_TIER1_NEFF = 15m;
    private const decimal CAP_TIER2_NEFF = 30m;
    private const decimal CAP_TIER3_NEFF = 50m;
    private const int GATE_DISTINCT_USERS_START = 3;
    private const int GATE_DISTINCT_OPPONENTS_START = 2;
    private const decimal GATE_NEFF_START = 3m;
    private const decimal GATE_NEFF_FULL = 10m;
    private const decimal SINGLE_USER_CONFIDENCE_THRESHOLD = 0.7m;
    private const decimal SINGLE_USER_ADJUSTMENT_RATIO = 0.25m;
    private const decimal PER_USER_DIMINISHING_SCALE = 1.5m;
    private const decimal USER_WEIGHT_AGE_FULL = 60m;
    private const decimal USER_WEIGHT_MEDIA_FULL = 15m;

    private record VoteData(int Id, string UserId, int DeckLowId, int DeckHighId, ComparisonOutcome Outcome);

    private record RatingData(string UserId, int DeckId, int Rating);

    [Queue("stats")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
    public async Task ComputeAllAdjustments()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await using var userContext = await userContextFactory.CreateDbContextAsync();

        var votes = await context.DifficultyVotes
                                 .Where(v => v.IsValid)
                                 .Select(v => new VoteData(v.Id, v.UserId, v.DeckLowId, v.DeckHighId, v.Outcome))
                                 .ToListAsync();

        var ratings = await context.DifficultyRatings
                                   .Select(r => new RatingData(r.UserId, r.DeckId, r.Rating))
                                   .ToListAsync();

        var referencedDeckIds = votes
                                .SelectMany(v => new[] { v.DeckLowId, v.DeckHighId })
                                .Union(ratings.Select(r => r.DeckId))
                                .ToHashSet();

        var deckInfo = await context.Decks
                                    .Where(d => referencedDeckIds.Contains(d.DeckId))
                                    .Select(d => new { d.DeckId, d.MediaType })
                                    .ToDictionaryAsync(d => d.DeckId, d => d.MediaType);

        var mlDifficulties = await context.DeckDifficulties
                                          .Where(dd => referencedDeckIds.Contains(dd.DeckId))
                                          .ToDictionaryAsync(dd => dd.DeckId, dd => dd.Difficulty);

        var decksWithMl = mlDifficulties.Keys.ToHashSet();
        var filteredVotes = votes
                            .Where(v => decksWithMl.Contains(v.DeckLowId) && decksWithMl.Contains(v.DeckHighId))
                            .ToList();
        var filteredRatings = ratings
                              .Where(r => decksWithMl.Contains(r.DeckId))
                              .ToList();

        var userDeckPairs = new HashSet<(string, int)>();
        foreach (var v in filteredVotes)
        {
            userDeckPairs.Add((v.UserId, v.DeckLowId));
            userDeckPairs.Add((v.UserId, v.DeckHighId));
        }

        foreach (var r in filteredRatings)
            userDeckPairs.Add((r.UserId, r.DeckId));

        var relevantUserIds = userDeckPairs.Select(p => p.Item1).Distinct().ToHashSet();
        var userCreationDates = await userContext.Users
                                                 .Where(u => relevantUserIds.Contains(u.Id))
                                                 .Select(u => new { u.Id, u.CreatedAt })
                                                 .ToDictionaryAsync(u => u.Id, u => u.CreatedAt);

        var uniqueMediaPerUser = new Dictionary<string, int>();
        foreach (var group in userDeckPairs.GroupBy(p => p.Item1))
            uniqueMediaPerUser[group.Key] = group.Count();

        var now = DateTime.UtcNow;
        var userWeights = new Dictionary<string, decimal>();
        foreach (var userId in userDeckPairs.Select(p => p.Item1).Distinct())
        {
            decimal ageFactor = 1m;
            if (userCreationDates.TryGetValue(userId, out var created))
            {
                var ageDays = (decimal)(now - created).TotalDays;
                ageFactor = Math.Min(1m, ageDays / USER_WEIGHT_AGE_FULL);
            }

            var mediaCount = uniqueMediaPerUser.GetValueOrDefault(userId, 0);
            var mediaFactor = Math.Min(1m, (decimal)Math.Sqrt((double)mediaCount / (double)USER_WEIGHT_MEDIA_FULL));
            userWeights[userId] = ageFactor * mediaFactor;
        }

        var voteTypeWeights = filteredVotes
                              .Select(v => MediaTypeGroups.GetComparisonWeight(
                                                                               deckInfo.GetValueOrDefault(v.DeckLowId),
                                                                               deckInfo.GetValueOrDefault(v.DeckHighId)))
                              .ToList();

        var finalVoteWeights = new decimal[filteredVotes.Count];
        var userDeckCumulative = new Dictionary<(string, int), decimal>();
        for (var i = 0; i < filteredVotes.Count; i++)
        {
            var v = filteredVotes[i];
            var wType = voteTypeWeights[i];
            var wUser = userWeights.GetValueOrDefault(v.UserId, 1m);

            var keyLow = (v.UserId, v.DeckLowId);
            var keyHigh = (v.UserId, v.DeckHighId);
            var cumLow = userDeckCumulative.GetValueOrDefault(keyLow, 0m);
            var cumHigh = userDeckCumulative.GetValueOrDefault(keyHigh, 0m);
            var dimLow = 1m / (1m + cumLow / PER_USER_DIMINISHING_SCALE);
            var dimHigh = 1m / (1m + cumHigh / PER_USER_DIMINISHING_SCALE);
            var wDim = Math.Min(dimLow, dimHigh);

            finalVoteWeights[i] = wType * wUser * wDim;

            userDeckCumulative[keyLow] = cumLow + finalVoteWeights[i];
            userDeckCumulative[keyHigh] = cumHigh + finalVoteWeights[i];
        }

        var allDeckIds = filteredVotes
                         .SelectMany(v => new[] { v.DeckLowId, v.DeckHighId })
                         .Union(filteredRatings.Select(r => r.DeckId))
                         .ToHashSet();
        var adj = new Dictionary<int, decimal>();
        foreach (var id in allDeckIds)
            adj[id] = 0m;

        var neffCache = new Dictionary<int, decimal>();
        foreach (var id in allDeckIds)
            neffCache[id] = 0m;
        for (var i = 0; i < filteredVotes.Count; i++)
        {
            var v = filteredVotes[i];
            neffCache[v.DeckLowId] += finalVoteWeights[i];
            neffCache[v.DeckHighId] += finalVoteWeights[i];
        }

        for (var iter = 0; iter < ITERATIONS; iter++)
        {
            for (var i = 0; i < filteredVotes.Count; i++)
            {
                var v = filteredVotes[i];
                var w = finalVoteWeights[i];
                if (w <= 0) continue;

                if (v.Outcome == ComparisonOutcome.Same)
                {
                    var dLow = mlDifficulties[v.DeckLowId] + adj[v.DeckLowId];
                    var dHigh = mlDifficulties[v.DeckHighId] + adj[v.DeckHighId];
                    var diff = (dLow - dHigh) / LOGISTIC_SCALE_S;
                    adj[v.DeckLowId] -= LEARNING_RATE * 0.5m * w * diff;
                    adj[v.DeckHighId] += LEARNING_RATE * 0.5m * w * diff;
                }
                else
                {
                    var multiplier = Math.Abs((int)v.Outcome) == 2 ? 2.0m : 1.0m;
                    var (easierDeckId, harderDeckId) = ResolveDirection(v);

                    var dEasier = mlDifficulties[easierDeckId] + adj[easierDeckId];
                    var dHarder = mlDifficulties[harderDeckId] + adj[harderDeckId];
                    var p = Sigmoid((dHarder - dEasier) / LOGISTIC_SCALE_S);
                    var grad = multiplier * (1m - p);
                    adj[easierDeckId] -= LEARNING_RATE * w * grad;
                    adj[harderDeckId] += LEARNING_RATE * w * grad;
                }
            }

            foreach (var r in filteredRatings)
            {
                var target = r.Rating + 0.5m;
                var current = mlDifficulties[r.DeckId] + adj[r.DeckId];
                var wUser = userWeights.GetValueOrDefault(r.UserId, 1m);
                adj[r.DeckId] += LEARNING_RATE * ABSOLUTE_WEIGHT * wUser * (target - current) / LOGISTIC_SCALE_S;
            }

            foreach (var id in allDeckIds)
            {
                adj[id] *= 1m - LEARNING_RATE * REGULARIZATION;
                var cap = ComputeDynamicCap(neffCache[id]);
                adj[id] = Math.Clamp(adj[id], -cap, cap);
            }
        }

        var easierCounts = new Dictionary<int, int>();
        var harderCounts = new Dictionary<int, int>();
        var deckUsers = new Dictionary<int, HashSet<string>>();
        var deckOpponents = new Dictionary<int, HashSet<int>>();
        foreach (var id in allDeckIds)
        {
            easierCounts[id] = 0;
            harderCounts[id] = 0;
            deckUsers[id] = [];
            deckOpponents[id] = [];
        }

        foreach (var v in filteredVotes)
        {
            deckUsers[v.DeckLowId].Add(v.UserId);
            deckUsers[v.DeckHighId].Add(v.UserId);
            deckOpponents[v.DeckLowId].Add(v.DeckHighId);
            deckOpponents[v.DeckHighId].Add(v.DeckLowId);

            if (v.Outcome != ComparisonOutcome.Same)
            {
                var (easierDeckId, harderDeckId) = ResolveDirection(v);
                easierCounts[easierDeckId] = easierCounts.GetValueOrDefault(easierDeckId) + 1;
                harderCounts[harderDeckId] = harderCounts.GetValueOrDefault(harderDeckId) + 1;
            }
        }

        var updates = new List<(int DeckId, int Easier, int Harder, decimal Adjustment, decimal Neff)>();
        foreach (var id in allDeckIds)
        {
            var neff = neffCache[id];
            var users = deckUsers[id].Count;
            var opponents = deckOpponents[id].Count;
            decimal adjDisplay;

            if (users >= GATE_DISTINCT_USERS_START &&
                opponents >= GATE_DISTINCT_OPPONENTS_START &&
                neff >= GATE_NEFF_START)
            {
                var scale = Math.Clamp(neff / GATE_NEFF_FULL, 0m, 1m);
                adjDisplay = adj[id] * scale;
            }
            else if (users >= 1 && users <= 2)
            {
                var maxUserConfidence = 0m;
                foreach (var userId in deckUsers[id])
                {
                    var wUser = userWeights.GetValueOrDefault(userId, 0m);
                    if (wUser > maxUserConfidence)
                        maxUserConfidence = wUser;
                }

                adjDisplay = maxUserConfidence > SINGLE_USER_CONFIDENCE_THRESHOLD
                    ? adj[id] * SINGLE_USER_ADJUSTMENT_RATIO
                    : 0m;
            }
            else
            {
                adjDisplay = 0m;
            }

            updates.Add((id, easierCounts.GetValueOrDefault(id), harderCounts.GetValueOrDefault(id),
                         Math.Round(adjDisplay, 2), Math.Round(neffCache[id], 2)));
        }

        var deckDifficulties = await context.DeckDifficulties
                                            .Where(dd => allDeckIds.Contains(dd.DeckId))
                                            .ToDictionaryAsync(dd => dd.DeckId);

        foreach (var (deckId, easier, harder, adjustment, neff) in updates)
        {
            if (deckDifficulties.TryGetValue(deckId, out var dd))
            {
                dd.EasierVoteCount = easier;
                dd.HarderVoteCount = harder;
                dd.DistinctVoterCount = deckUsers.GetValueOrDefault(deckId)?.Count ?? 0;
                dd.UserAdjustment = adjustment;
                dd.NEffective = neff;
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Difficulty adjustment completed for {Count} decks", updates.Count);
    }

    private static (int easier, int harder) ResolveDirection(VoteData v) =>
        (int)v.Outcome < 0 ? (v.DeckLowId, v.DeckHighId) : (v.DeckHighId, v.DeckLowId);

    private static decimal ComputeDynamicCap(decimal neff)
    {
        if (neff < CAP_TIER1_NEFF)
            return MAX_ADJUSTMENT_BASE;
        if (neff < CAP_TIER2_NEFF)
            return MAX_ADJUSTMENT_BASE + 0.5m * (neff - CAP_TIER1_NEFF) / (CAP_TIER2_NEFF - CAP_TIER1_NEFF);
        if (neff < CAP_TIER3_NEFF)
            return 1.0m + 0.5m * (neff - CAP_TIER2_NEFF) / (CAP_TIER3_NEFF - CAP_TIER2_NEFF);

        return 1.5m + 0.5m * Math.Min(1m, (neff - CAP_TIER3_NEFF) / 50m);
    }

    private static decimal Sigmoid(decimal x)
    {
        var d = (double)x;
        return (decimal)(1.0 / (1.0 + Math.Exp(-d)));
    }
}