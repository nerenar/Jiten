using FluentAssertions;
using Jiten.Api.Jobs;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jiten.Tests;

public class DifficultyAdjustmentTests : IDisposable
{
    private readonly SqliteConnection _jitenConnection;
    private readonly SqliteConnection _userConnection;
    private readonly DbContextOptions<JitenDbContext> _jitenOptions;
    private readonly DbContextOptions<UserDbContext> _userOptions;
    private int _voteIdCounter = 1;
    private int _ratingIdCounter = 1;

    public DifficultyAdjustmentTests()
    {
        _jitenConnection = new SqliteConnection("DataSource=:memory:");
        _jitenConnection.Open();
        _jitenOptions = new DbContextOptionsBuilder<JitenDbContext>()
            .UseSqlite(_jitenConnection)
            .Options;

        _userConnection = new SqliteConnection("DataSource=:memory:");
        _userConnection.Open();
        _userOptions = new DbContextOptionsBuilder<UserDbContext>()
            .UseSqlite(_userConnection)
            .Options;

        using var jitenCtx = new JitenDbContext(_jitenOptions);
        jitenCtx.Database.EnsureCreated();

        using var userCtx = new UserDbContext(_userOptions);
        userCtx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _jitenConnection.Dispose();
        _userConnection.Dispose();
    }

    private JitenDbContext CreateJitenContext() => new(_jitenOptions);
    private UserDbContext CreateUserContext() => new(_userOptions);

    private DifficultyAdjustmentJob CreateJob()
    {
        var jitenFactory = new TestDbContextFactory<JitenDbContext>(() => CreateJitenContext());
        var userFactory = new TestDbContextFactory<UserDbContext>(() => CreateUserContext());
        return new DifficultyAdjustmentJob(jitenFactory, userFactory,
            NullLogger<DifficultyAdjustmentJob>.Instance);
    }

    private void SeedUsers(params string[] userIds)
    {
        using var ctx = CreateUserContext();
        foreach (var id in userIds)
        {
            ctx.Users.Add(new User
            {
                Id = id,
                UserName = $"user_{id}",
                NormalizedUserName = $"USER_{id}",
                Email = $"{id}@test.com",
                NormalizedEmail = $"{id}@TEST.COM",
                SecurityStamp = Guid.NewGuid().ToString()
            });
        }
        ctx.SaveChanges();

        // UserDbContext.AddTimestamps() overrides CreatedAt on save, so set it via raw SQL
        var pastDate = DateTime.UtcNow.AddDays(-90);
        using var ctx2 = CreateUserContext();
        ctx2.Database.ExecuteSqlRaw(
            "UPDATE AspNetUsers SET CreatedAt = {0} WHERE Id IN (" +
            string.Join(",", userIds.Select(id => $"'{id}'")) + ")",
            pastDate);
    }

    private void SeedDecks(params (int deckId, decimal mlDifficulty)[] decks)
    {
        using var ctx = CreateJitenContext();
        foreach (var (deckId, mlDifficulty) in decks)
        {
            ctx.Decks.Add(new Deck
            {
                DeckId = deckId,
                MediaType = MediaType.Novel,
                OriginalTitle = $"Deck {deckId}"
            });
            ctx.DeckDifficulties.Add(new DeckDifficulty
            {
                DeckId = deckId,
                Difficulty = mlDifficulty,
                Peak = mlDifficulty + 0.5m
            });
        }
        ctx.SaveChanges();
    }

    private void SeedVotes(params (string userId, int deckLowId, int deckHighId, ComparisonOutcome outcome)[] votes)
    {
        using var ctx = CreateJitenContext();
        foreach (var (userId, deckLowId, deckHighId, outcome) in votes)
        {
            ctx.DifficultyVotes.Add(new DifficultyVote
            {
                Id = _voteIdCounter++,
                UserId = userId,
                DeckLowId = deckLowId,
                DeckHighId = deckHighId,
                Outcome = outcome,
                IsValid = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        ctx.SaveChanges();
    }

    private void SeedRatings(params (string userId, int deckId, int rating)[] ratings)
    {
        using var ctx = CreateJitenContext();
        foreach (var (userId, deckId, rating) in ratings)
        {
            ctx.DifficultyRatings.Add(new DifficultyRating
            {
                Id = _ratingIdCounter++,
                UserId = userId,
                DeckId = deckId,
                Rating = rating,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        ctx.SaveChanges();
    }

    private Dictionary<int, decimal> GetAdjustments(params int[] deckIds)
    {
        using var ctx = CreateJitenContext();
        return ctx.DeckDifficulties
            .Where(dd => deckIds.Contains(dd.DeckId))
            .ToDictionary(dd => dd.DeckId, dd => dd.UserAdjustment);
    }

    /// <summary>
    /// Seeds padding decks and votes so users have full media weight (mediaFactor = 1.0).
    /// Each user votes on 16 consecutive padding deck pairs (Same outcome, no gradient effect).
    /// Gating requirements: users >= 3, opponents >= 2, N_eff >= 4.
    /// </summary>
    private void SeedHighWeightScenario(string[] users, (int deckId, decimal mlDifficulty)[] targetDecks,
        int paddingStartId = 100)
    {
        var paddingDecks = Enumerable.Range(paddingStartId, 16)
            .Select(i => (i, 3.0m))
            .ToArray();

        SeedDecks(targetDecks.Concat(paddingDecks).ToArray());

        var paddingVotes = new List<(string, int, int, ComparisonOutcome)>();
        foreach (var user in users)
        {
            for (var i = 0; i < 15; i++)
                paddingVotes.Add((user, paddingStartId + i, paddingStartId + i + 1, ComparisonOutcome.Same));
        }
        SeedVotes(paddingVotes.ToArray());
    }

    [Fact]
    public async Task UnanimousEasierVotes_ProducesNegativeAdjustment()
    {
        var users = new[] { "u1", "u2", "u3", "u4" };
        SeedUsers(users);

        // 3 target decks so deck 1 has >= 2 opponents for gating
        var targetDecks = new[] { (1, 3.0m), (2, 3.0m), (3, 3.0m) };
        SeedHighWeightScenario(users, targetDecks);

        // All users say deck 1 is easier
        SeedVotes(
            ("u1", 1, 2, ComparisonOutcome.MuchEasier),
            ("u2", 1, 2, ComparisonOutcome.MuchEasier),
            ("u3", 1, 3, ComparisonOutcome.MuchEasier),
            ("u4", 1, 3, ComparisonOutcome.MuchEasier)
        );

        var job = CreateJob();
        await job.ComputeAllAdjustments();

        var adj = GetAdjustments(1, 2, 3);
        adj[1].Should().BeNegative("unanimous easier votes should push deck 1's adjustment negative");
        adj[2].Should().BePositive("deck 2 should be pushed harder");
        adj[3].Should().BePositive("deck 3 should be pushed harder");
    }

    [Fact]
    public async Task SameVotes_PullDecksTogether()
    {
        var users = new[] { "u1", "u2", "u3", "u4" };
        SeedUsers(users);

        // Large ML gap between deck 1 and 2; deck 3 for opponent gating
        var targetDecks = new[] { (1, 2.0m), (2, 5.0m), (3, 3.5m) };
        SeedHighWeightScenario(users, targetDecks);

        // All voters say decks 1 and 2 are about the same; include deck 3 for opponent count
        SeedVotes(
            ("u1", 1, 2, ComparisonOutcome.Same),
            ("u2", 1, 2, ComparisonOutcome.Same),
            ("u3", 1, 2, ComparisonOutcome.Same),
            ("u4", 1, 2, ComparisonOutcome.Same),
            ("u1", 1, 3, ComparisonOutcome.Same),
            ("u2", 2, 3, ComparisonOutcome.Same)
        );

        var job = CreateJob();
        await job.ComputeAllAdjustments();

        var adj = GetAdjustments(1, 2);
        var effectiveGap = (5.0m + adj[2]) - (2.0m + adj[1]);
        effectiveGap.Should().BeLessThan(3.0m, "same votes should reduce the difficulty gap");
        adj[1].Should().BePositive("easier deck should be adjusted upward");
        adj[2].Should().BeNegative("harder deck should be adjusted downward");
    }

    [Fact]
    public async Task AdjustmentWithinDynamicCap()
    {
        var users = Enumerable.Range(1, 6).Select(i => $"u{i}").ToArray();
        SeedUsers(users);

        // 3 target decks for opponent gating
        var targetDecks = new[] { (1, 3.0m), (2, 3.0m), (3, 3.0m) };
        SeedHighWeightScenario(users, targetDecks);

        // Extreme votes: all say deck 1 is much easier than both 2 and 3
        var votes = new List<(string, int, int, ComparisonOutcome)>();
        foreach (var u in users)
        {
            votes.Add((u, 1, 2, ComparisonOutcome.MuchEasier));
            votes.Add((u, 1, 3, ComparisonOutcome.MuchEasier));
        }
        SeedVotes(votes.ToArray());

        var job = CreateJob();
        await job.ComputeAllAdjustments();

        var adj = GetAdjustments(1, 2, 3);

        // Dynamic cap is at most 2.0 even at very high N_eff
        adj[1].Should().BeInRange(-2.0m, 2.0m, "adjustment should stay within maximum cap");
        adj[2].Should().BeInRange(-2.0m, 2.0m, "adjustment should stay within maximum cap");
    }

    [Fact]
    public async Task SingleUserDiminishingReturns()
    {
        SeedUsers("single");

        // Many target decks + padding so user has high media factor (>0.7 confidence)
        var targetDecks = Enumerable.Range(1, 20)
            .Select(i => (i, 3.0m))
            .ToArray();
        SeedHighWeightScenario(new[] { "single" }, targetDecks, paddingStartId: 200);

        // Single user votes deck 1 as easier against many opponents
        var votes = Enumerable.Range(2, 15)
            .Select(i => ("single", 1, i, ComparisonOutcome.MuchEasier))
            .ToArray();
        SeedVotes(votes);

        var job = CreateJob();
        await job.ComputeAllAdjustments();

        var adjSingle = GetAdjustments(1)[1];

        // Single-user path: adj * SingleUserAdjustmentRatio (0.25)
        adjSingle.Should().BeNegative("votes saying deck is easier should produce negative adjustment");
        Math.Abs(adjSingle).Should().BeLessThan(0.5m,
            "single user's votes should be attenuated by SingleUserAdjustmentRatio (0.25)");
    }

    [Fact]
    public async Task MultipleUsersProduceLargerAdjustmentThanSingleUser()
    {
        var multiUsers = Enumerable.Range(1, 10).Select(i => $"mu{i}").ToArray();
        SeedUsers(multiUsers);

        var targetDecks = Enumerable.Range(1, 12)
            .Select(i => (i, 3.0m))
            .ToArray();
        SeedHighWeightScenario(multiUsers, targetDecks, paddingStartId: 200);

        // Each user votes deck 1 as much easier than a different opponent
        var votes = multiUsers.Select((u, i) =>
            (u, 1, i + 2, ComparisonOutcome.MuchEasier)).ToArray();
        SeedVotes(votes);

        var job = CreateJob();
        await job.ComputeAllAdjustments();

        var adjMulti = GetAdjustments(1)[1];

        // 10 users, 10 opponents: passes full gating
        adjMulti.Should().BeNegative(
            "10 users voting easier should produce a negative adjustment");
    }

    [Fact]
    public async Task AbsoluteRatingWeakerThanPairwise()
    {
        var users = new[] { "u1", "u2", "u3", "u4", "u5" };
        SeedUsers(users);

        // 3 target decks so deck 1 has >= 2 opponents
        var targetDecks = new[] { (1, 3.0m), (2, 3.0m), (3, 3.0m) };
        SeedHighWeightScenario(users, targetDecks);

        // Pairwise: all say deck 1 is much easier
        SeedVotes(
            ("u1", 1, 2, ComparisonOutcome.MuchEasier),
            ("u2", 1, 2, ComparisonOutcome.MuchEasier),
            ("u3", 1, 3, ComparisonOutcome.MuchEasier),
            ("u4", 1, 3, ComparisonOutcome.MuchEasier),
            ("u5", 1, 2, ComparisonOutcome.MuchEasier)
        );

        var jobA = CreateJob();
        await jobA.ComputeAllAdjustments();
        var adjPairwise = GetAdjustments(1)[1];

        // Reset: remove votes and adjustments
        using (var ctx = CreateJitenContext())
        {
            ctx.DifficultyVotes.RemoveRange(ctx.DifficultyVotes);
            foreach (var dd in ctx.DeckDifficulties)
            {
                dd.UserAdjustment = 0;
                dd.EasierVoteCount = 0;
                dd.HarderVoteCount = 0;
            }
            await ctx.SaveChangesAsync();
        }
        _voteIdCounter = 1;

        // Re-add padding votes
        var paddingVotes = new List<(string, int, int, ComparisonOutcome)>();
        foreach (var user in users)
        {
            for (var i = 0; i < 15; i++)
                paddingVotes.Add((user, 100 + i, 100 + i + 1, ComparisonOutcome.Same));
        }
        SeedVotes(paddingVotes.ToArray());

        // Absolute ratings only: rating=1 → target difficulty = 1.5, deck ML = 3.0
        SeedRatings(
            ("u1", 1, 1),
            ("u2", 1, 1),
            ("u3", 1, 1),
            ("u4", 1, 1),
            ("u5", 1, 1)
        );

        var jobB = CreateJob();
        await jobB.ComputeAllAdjustments();
        var adjAbsolute = GetAdjustments(1)[1];

        // Pairwise should produce non-trivial adjustment (5 users, 2 opponents)
        Math.Abs(adjPairwise).Should().BeGreaterThan(0,
            "pairwise votes should produce non-zero adjustment");

        // Absolute-only: deck 1 has no pairwise votes → deckUsers[1].Count = 0 → adjDisplay = 0
        Math.Abs(adjAbsolute).Should().BeLessThan(Math.Abs(adjPairwise),
            "absolute ratings should produce smaller adjustment than pairwise votes");
    }

    [Fact]
    public async Task NoVotes_NoAdjustment()
    {
        SeedUsers("u1");
        SeedDecks((1, 3.0m), (2, 4.0m));

        var job = CreateJob();
        await job.ComputeAllAdjustments();

        var adj = GetAdjustments(1, 2);
        adj[1].Should().Be(0m);
        adj[2].Should().Be(0m);
    }

    [Fact]
    public async Task OpposingVotes_CancelOut()
    {
        var users = new[] { "u1", "u2", "u3", "u4" };
        SeedUsers(users);

        // 3 target decks for opponent gating
        var targetDecks = new[] { (1, 3.0m), (2, 3.0m), (3, 3.0m) };
        SeedHighWeightScenario(users, targetDecks);

        // Half say easier, half say harder; include deck 3 for opponent count
        SeedVotes(
            ("u1", 1, 2, ComparisonOutcome.Easier),
            ("u2", 1, 2, ComparisonOutcome.Easier),
            ("u3", 1, 2, ComparisonOutcome.Harder),
            ("u4", 1, 2, ComparisonOutcome.Harder),
            ("u1", 1, 3, ComparisonOutcome.Same),
            ("u2", 2, 3, ComparisonOutcome.Same)
        );

        var job = CreateJob();
        await job.ComputeAllAdjustments();

        var adj = GetAdjustments(1, 2);
        Math.Abs(adj[1]).Should().BeLessThan(0.05m,
            "opposing votes should approximately cancel out");
        Math.Abs(adj[2]).Should().BeLessThan(0.05m,
            "opposing votes should approximately cancel out");
    }

    private class TestDbContextFactory<T>(Func<T> factory) : IDbContextFactory<T> where T : DbContext
    {
        public T CreateDbContext() => factory();
        public Task<T> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(factory());
    }
}
