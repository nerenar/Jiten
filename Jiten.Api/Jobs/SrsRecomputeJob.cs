using Hangfire;
using Jiten.Api.Dtos;
using Jiten.Core;
using Jiten.Core.Data.FSRS;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Jobs;

public class SrsRecomputeJob(
    IDbContextFactory<UserDbContext> userContextFactory,
    ILogger<SrsRecomputeJob> logger)
{
    private const int BatchSize = 500;

    [Queue("default")]
    public async Task RecomputeUserSrs(string userId, double[] parameters, double desiredRetention, bool loadBalance = true)
    {
        // Single-shot recompute: one in-memory balancer accumulates across all batches, so every card is
        // placed against the freshly-rebalanced schedule built so far (online greedy balancing).
        var balancer = loadBalance ? new DictionaryFsrsLoadBalancer() : null;
        var lastCardId = 0L;

        while (true)
        {
            var result = await RecomputeUserSrsBatch(userId, parameters, desiredRetention, lastCardId, BatchSize, loadBalance, balancer);
            if (result.Processed == 0 || result.Done)
            {
                break;
            }

            lastCardId = result.LastCardId;
        }

        logger.LogInformation("Recomputed FSRS scheduling for user {UserId}", userId);
    }

    /// <param name="sharedBalancer">
    /// When provided (single-shot loop), used and accumulated across batches. When null and
    /// <paramref name="loadBalance"/> is true (stateless client-driven batches), a fresh balancer is seeded
    /// from the user's current schedule in the database — which already reflects prior batches' saved
    /// placements — so balancing still works across independent HTTP calls.
    /// </param>
    public async Task<SrsRecomputeBatchResponse> RecomputeUserSrsBatch(string userId, double[] parameters, double desiredRetention,
                                                                       long lastCardId, int batchSize, bool loadBalance = true,
                                                                       IFsrsLoadBalancer? sharedBalancer = null)
    {
        await using var userContext = await userContextFactory.CreateDbContextAsync();

        IFsrsLoadBalancer? balancer = null;
        if (loadBalance)
        {
            balancer = sharedBalancer ?? new DictionaryFsrsLoadBalancer(
                           await userContext.FsrsCards
                                             .AsNoTracking()
                                             .Where(c => c.UserId == userId
                                                         && c.Due > DateTime.UtcNow
                                                         && c.Due < DateTime.MaxValue
                                                         && (c.State == FsrsState.Review
                                                             || c.State == FsrsState.Relearning
                                                             || c.State == FsrsState.Learning))
                                             .Select(c => c.Due)
                                             .ToListAsync());
        }

        var scheduler = new FsrsScheduler(desiredRetention: desiredRetention, parameters: parameters, loadBalancer: balancer);

        var total = await userContext.FsrsCards.CountAsync(card => card.UserId == userId);
        var cards = await userContext.FsrsCards
                                     .Where(card => card.UserId == userId && card.CardId > lastCardId)
                                     .OrderBy(card => card.CardId)
                                     .Take(batchSize)
                                     .ToListAsync();

        if (cards.Count == 0)
        {
            return new SrsRecomputeBatchResponse
            {
                Processed = 0,
                Total = total,
                LastCardId = lastCardId,
                Done = true
            };
        }

        var cardIds = cards.Select(card => card.CardId).ToList();
        var logs = await userContext.FsrsReviewLogs
                                    .AsNoTracking()
                                    .Where(log => cardIds.Contains(log.CardId))
                                    .OrderBy(log => log.ReviewDateTime)
                                    .ThenBy(log => log.ReviewLogId)
                                    .ToListAsync();

        var logsByCard = logs.GroupBy(log => log.CardId)
                             .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var card in cards)
        {
            if (!logsByCard.TryGetValue(card.CardId, out var cardLogs) || cardLogs.Count == 0)
            {
                continue;
            }

            var overrideState = card.State is FsrsState.Mastered or FsrsState.Blacklisted or FsrsState.Suspended
                ? card.State
                : (FsrsState?)null;

            var tempCard = new FsrsCard(card.UserId, card.WordId, card.ReadingIndex);
            var lapses = 0;
            foreach (var log in cardLogs)
            {
                var prevState = tempCard.State;
                var review = scheduler.ReviewCard(tempCard, log.Rating, log.ReviewDateTime, log.ReviewDuration);
                if (prevState == FsrsState.Review && log.Rating == FsrsRating.Again)
                    lapses++;
                tempCard = review.UpdatedCard;
            }

            card.State = tempCard.State;
            card.Step = tempCard.Step;
            card.Stability = tempCard.Stability;
            card.Difficulty = tempCard.Difficulty;
            card.LastReview = tempCard.LastReview;
            card.Due = tempCard.Due;
            card.Lapses = lapses;

            if (overrideState != null)
            {
                card.State = overrideState.Value;
            }
        }

        var newLastCardId = cards[^1].CardId;
        await userContext.SaveChangesAsync();
        userContext.ChangeTracker.Clear();

        return new SrsRecomputeBatchResponse
        {
            Processed = cards.Count,
            Total = total,
            LastCardId = newLastCardId,
            Done = cards.Count < batchSize
        };
    }
}
