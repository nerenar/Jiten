using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jiten.Api.Dtos;
using Jiten.Api.Jobs;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.Authentication;
using Jiten.Parser.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jiten.Parser.Tests.Integration;

public class DifficultyVoteTests(JitenWebApplicationFactory factory)
    : IClassFixture<JitenWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private const string NewUser = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    private async Task SeedTestData(bool includeNewUser = false)
    {
        using var scope = factory.Services.CreateScope();
        var jitenDb = scope.ServiceProvider.GetRequiredService<JitenDbContext>();
        var userDb = scope.ServiceProvider.GetRequiredService<UserDbContext>();

        jitenDb.DifficultyVotes.RemoveRange(jitenDb.DifficultyVotes);
        jitenDb.DifficultyRatings.RemoveRange(jitenDb.DifficultyRatings);
        jitenDb.SkippedComparisons.RemoveRange(jitenDb.SkippedComparisons);
        jitenDb.DeckDifficulties.RemoveRange(jitenDb.DeckDifficulties);
        userDb.UserDeckPreferences.RemoveRange(userDb.UserDeckPreferences);
        var existingDecks = jitenDb.Decks.ToList();
        jitenDb.Decks.RemoveRange(existingDecks);
        var existingUsers = userDb.Users.ToList();
        userDb.Users.RemoveRange(existingUsers);
        await jitenDb.SaveChangesAsync();
        await userDb.SaveChangesAsync();

        var userA = new User
        {
            Id = TestUsers.UserA,
            UserName = "userA",
            NormalizedUserName = "USERA",
            Email = "a@test.com",
            NormalizedEmail = "A@TEST.COM",
            TosAcceptedAt = DateTime.UtcNow.AddDays(-30)
        };
        var userB = new User
        {
            Id = TestUsers.UserB,
            UserName = "userB",
            NormalizedUserName = "USERB",
            Email = "b@test.com",
            NormalizedEmail = "B@TEST.COM",
            TosAcceptedAt = DateTime.UtcNow.AddDays(-30)
        };
        var admin = new User
        {
            Id = TestUsers.Admin,
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@test.com",
            NormalizedEmail = "ADMIN@TEST.COM",
            TosAcceptedAt = DateTime.UtcNow.AddDays(-30)
        };
        userDb.Users.AddRange(userA, userB, admin);

        if (includeNewUser)
        {
            var newUser = new User
            {
                Id = NewUser,
                UserName = "newUser",
                NormalizedUserName = "NEWUSER",
                Email = "new@test.com",
                NormalizedEmail = "NEW@TEST.COM",
                TosAcceptedAt = DateTime.UtcNow.AddDays(-1)
            };
            userDb.Users.Add(newUser);
        }

        // Save first to let AddTimestamps set CreatedAt to now, then backdate
        await userDb.SaveChangesAsync();
        userA.CreatedAt = DateTime.UtcNow.AddDays(-90);
        userB.CreatedAt = DateTime.UtcNow.AddDays(-90);
        admin.CreatedAt = DateTime.UtcNow.AddDays(-90);
        if (includeNewUser)
        {
            var newUserEntity = await userDb.Users.FindAsync(NewUser);
            newUserEntity!.CreatedAt = DateTime.UtcNow.AddDays(-1);
        }

        // Decks 1,2,4,5,6: Anime (AudioVisual group), comparable
        // Deck 3: Novel (Prose group), NOT comparable with Anime
        var decks = new[]
        {
            new Deck { DeckId = 1, OriginalTitle = "Anime A", MediaType = MediaType.Anime, Difficulty = 2.0f },
            new Deck { DeckId = 2, OriginalTitle = "Anime B", MediaType = MediaType.Anime, Difficulty = 3.0f },
            new Deck { DeckId = 3, OriginalTitle = "Novel A", MediaType = MediaType.Novel, Difficulty = 2.0f },
            new Deck { DeckId = 4, OriginalTitle = "Anime C", MediaType = MediaType.Anime, Difficulty = 4.0f },
            new Deck { DeckId = 5, OriginalTitle = "Anime D", MediaType = MediaType.Anime, Difficulty = 2.5f },
            new Deck { DeckId = 6, OriginalTitle = "Anime E", MediaType = MediaType.Anime, Difficulty = 3.5f }
        };
        jitenDb.Decks.AddRange(decks);

        jitenDb.DeckDifficulties.AddRange(
            new DeckDifficulty { DeckId = 1, Difficulty = 2.0m, Peak = 2.5m },
            new DeckDifficulty { DeckId = 2, Difficulty = 3.0m, Peak = 3.5m },
            new DeckDifficulty { DeckId = 3, Difficulty = 2.0m, Peak = 2.5m },
            new DeckDifficulty { DeckId = 4, Difficulty = 4.0m, Peak = 4.5m },
            new DeckDifficulty { DeckId = 5, Difficulty = 2.5m, Peak = 3.0m },
            new DeckDifficulty { DeckId = 6, Difficulty = 3.5m, Peak = 4.0m }
        );

        var prefs = new List<UserDeckPreference>();
        foreach (var userId in new[] { TestUsers.UserA, TestUsers.UserB, TestUsers.Admin })
            foreach (var deckId in new[] { 1, 2, 4, 5, 6 })
                prefs.Add(new UserDeckPreference { UserId = userId, DeckId = deckId, Status = DeckStatus.Completed });
        prefs.Add(new UserDeckPreference { UserId = TestUsers.UserA, DeckId = 3, Status = DeckStatus.Completed });
        userDb.UserDeckPreferences.AddRange(prefs);

        await jitenDb.SaveChangesAsync();
        await userDb.SaveChangesAsync();
    }

    // --- 7C Tests ---

    [Fact]
    public async Task SubmitVote_SwappedPair_CanonicalizedCorrectly()
    {
        await SeedTestData();

        // Submit with DeckAId=2 > DeckBId=1, outcome=Harder (DeckA is harder)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckAId = 2, deckBId = 1, outcome = (int)ComparisonOutcome.Harder });
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var voteId = body.GetProperty("id").GetInt32();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JitenDbContext>();
        var vote = await db.DifficultyVotes.FindAsync(voteId);
        vote.Should().NotBeNull();
        vote!.DeckLowId.Should().Be(1);
        vote.DeckHighId.Should().Be(2);
        // Harder for DeckA(2) means DeckHigh is harder, so outcome should be flipped to Easier (-1)
        // Original outcome=Harder(1), DeckAId(2)>DeckBId(1), so outcome = -(1) = Easier(-1)
        vote.Outcome.Should().Be(ComparisonOutcome.Easier);
    }

    [Fact]
    public async Task SubmitVote_DuplicateCanonicalPair_UpsertsOutcome()
    {
        await SeedTestData();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckAId = 1, deckBId = 2, outcome = (int)ComparisonOutcome.Easier });
        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        var body1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        body1.GetProperty("isUpdate").GetBoolean().Should().BeFalse();

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckAId = 1, deckBId = 2, outcome = (int)ComparisonOutcome.Harder });
        var response2 = await _client.SendAsync(request2);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var body2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        body2.GetProperty("isUpdate").GetBoolean().Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JitenDbContext>();
        var voteCount = await db.DifficultyVotes.CountAsync(v => v.UserId == TestUsers.UserA && v.DeckLowId == 1 && v.DeckHighId == 2);
        voteCount.Should().Be(1);
    }

    [Fact]
    public async Task SubmitVote_IncomparableMediaTypes_Returns400()
    {
        await SeedTestData();

        // Anime(1) vs Novel(3) are incomparable (AudioVisual vs Prose)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckAId = 1, deckBId = 3, outcome = (int)ComparisonOutcome.Easier });
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RankingMove_ReaddingDeckToGroup_PutsItAtTop()
    {
        await SeedTestData();

        int rankGroupId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JitenDbContext>();
            var group = new DifficultyRankGroup
            {
                UserId = TestUsers.UserA,
                MediaTypeGroup = MediaTypeGroup.AudioVisual,
                SortIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
            };
            db.DifficultyRankGroups.Add(group);
            await db.SaveChangesAsync();

            rankGroupId = group.Id;
            db.DifficultyRankItems.AddRange(
                new DifficultyRankItem
                {
                    UserId = TestUsers.UserA,
                    GroupId = rankGroupId,
                    DeckId = 1,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                    UpdatedAt = DateTimeOffset.UtcNow.AddDays(-10)
                },
                new DifficultyRankItem
                {
                    UserId = TestUsers.UserA,
                    GroupId = rankGroupId,
                    DeckId = 2,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
                    UpdatedAt = DateTimeOffset.UtcNow.AddDays(-5)
                });
            await db.SaveChangesAsync();
        }

        var unrankRequest = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-rankings/move")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckId = 1, mode = (int)DifficultyRankingMoveMode.Unrank });
        var unrankResponse = await _client.SendAsync(unrankRequest);
        unrankResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var mergeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-rankings/move")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckId = 1, mode = (int)DifficultyRankingMoveMode.Merge, targetGroupId = rankGroupId });
        var mergeResponse = await _client.SendAsync(mergeRequest);
        mergeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await mergeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var audioVisualSection = body.EnumerateArray().First(s => s.GetProperty("group").GetInt32() == (int)MediaTypeGroup.AudioVisual);
        var firstGroup = audioVisualSection.GetProperty("groups")[0];
        var firstDeckId = firstGroup.GetProperty("decks")[0].GetProperty("id").GetInt32();
        firstDeckId.Should().Be(1);
    }

    [Fact]
    public async Task SubmitVote_NotCompletedDeck_Returns403()
    {
        await SeedTestData();

        // UserB has NOT completed deck 3
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(TestUsers.UserB)
            .WithJsonContent(new { deckAId = 1, deckBId = 3, outcome = (int)ComparisonOutcome.Easier });
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SubmitRating_UpsertNoDuplicate()
    {
        await SeedTestData();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes/rating")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckId = 1, rating = 2 });
        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes/rating")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckId = 1, rating = 4 });
        var response2 = await _client.SendAsync(request2);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JitenDbContext>();
        var ratings = await db.DifficultyRatings.Where(r => r.UserId == TestUsers.UserA && r.DeckId == 1).ToListAsync();
        ratings.Should().HaveCount(1);
        ratings[0].Rating.Should().Be(4);
    }

    [Fact]
    public async Task SubmitVote_NewAccount_Returns403()
    {
        await SeedTestData(includeNewUser: true);

        // NewUser account is only 1 day old (requires 7)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(NewUser)
            .WithJsonContent(new { deckAId = 1, deckBId = 2, outcome = (int)ComparisonOutcome.Easier });
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteOwnVote_Returns204()
    {
        await SeedTestData();

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckAId = 1, deckBId = 2, outcome = (int)ComparisonOutcome.Easier });
        var createResponse = await _client.SendAsync(createRequest);
        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var voteId = body.GetProperty("id").GetInt32();

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/difficulty-votes/{voteId}")
            .WithUser(TestUsers.UserA);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JitenDbContext>();
        var vote = await db.DifficultyVotes.FindAsync(voteId);
        vote.Should().BeNull();
    }

    [Fact]
    public async Task DeleteOthersVote_Returns404()
    {
        await SeedTestData();

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckAId = 1, deckBId = 2, outcome = (int)ComparisonOutcome.Easier });
        var createResponse = await _client.SendAsync(createRequest);
        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var voteId = body.GetProperty("id").GetInt32();

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/difficulty-votes/{voteId}")
            .WithUser(TestUsers.UserB);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SkipPair_ExcludedFromSuggestions()
    {
        await SeedTestData();

        // UserA has completed decks 1, 2, 4 (all Anime). Skip pair (1, 2).
        var skipRequest = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes/skip")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckAId = 1, deckBId = 2, permanent = true });
        var skipResponse = await _client.SendAsync(skipRequest);
        skipResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var suggestRequest = new HttpRequestMessage(HttpMethod.Get, "/api/difficulty-votes/suggestions?count=10")
            .WithUser(TestUsers.UserA);
        var suggestResponse = await _client.SendAsync(suggestRequest);
        suggestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var suggestions = await suggestResponse.Content.ReadFromJsonAsync<JsonElement>();
        var pairs = suggestions.EnumerateArray()
            .Select(s => (
                a: s.GetProperty("deckA").GetProperty("id").GetInt32(),
                b: s.GetProperty("deckB").GetProperty("id").GetInt32()))
            .ToList();

        pairs.Should().NotContain(p => (p.a == 1 && p.b == 2) || (p.a == 2 && p.b == 1));
    }

    [Fact]
    public async Task GetSuggestions_ExcludesAlreadyCompared()
    {
        await SeedTestData();

        // Vote on pair (1, 2)
        var voteRequest = new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { deckAId = 1, deckBId = 2, outcome = (int)ComparisonOutcome.Easier });
        var voteResponse = await _client.SendAsync(voteRequest);
        voteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var suggestRequest = new HttpRequestMessage(HttpMethod.Get, "/api/difficulty-votes/suggestions?count=10")
            .WithUser(TestUsers.UserA);
        var suggestResponse = await _client.SendAsync(suggestRequest);
        suggestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var suggestions = await suggestResponse.Content.ReadFromJsonAsync<JsonElement>();
        var pairs = suggestions.EnumerateArray()
            .Select(s => (
                a: s.GetProperty("deckA").GetProperty("id").GetInt32(),
                b: s.GetProperty("deckB").GetProperty("id").GetInt32()))
            .ToList();

        pairs.Should().NotContain(p => (p.a == 1 && p.b == 2) || (p.a == 2 && p.b == 1));
    }
    
    // --- 7D: End-to-end flow test ---

    [Fact]
    public async Task EndToEnd_VotesProduceAdjustment()
    {
        await SeedTestData();

        var users = new[] { TestUsers.UserA, TestUsers.UserB, TestUsers.Admin };
        // Consistent ranking: 1 < 5 < 2 < 6 < 4 (easy to hard)
        // Vote on all pairs to build up neff across all decks
        var pairsWithOutcome = new (int a, int b, ComparisonOutcome outcome)[]
        {
            (1, 2, ComparisonOutcome.MuchEasier),
            (1, 4, ComparisonOutcome.MuchEasier),
            (1, 5, ComparisonOutcome.Easier),
            (1, 6, ComparisonOutcome.MuchEasier),
            (2, 4, ComparisonOutcome.Easier),
            (2, 6, ComparisonOutcome.Easier),
            (5, 4, ComparisonOutcome.MuchEasier),
            (5, 6, ComparisonOutcome.Easier),
            (5, 2, ComparisonOutcome.Easier),
            (6, 4, ComparisonOutcome.Easier),
        };

        foreach (var (a, b, outcome) in pairsWithOutcome)
        {
            foreach (var user in users)
            {
                var msg = user == TestUsers.Admin
                    ? new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes").WithAdmin()
                    : new HttpRequestMessage(HttpMethod.Post, "/api/difficulty-votes").WithUser(user);
                msg.WithJsonContent(new { deckAId = a, deckBId = b, outcome = (int)outcome });
                (await _client.SendAsync(msg)).StatusCode.Should().Be(HttpStatusCode.Created);
            }
        }

        // Invoke background job
        using var scope = factory.Services.CreateScope();
        var jitenDbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<JitenDbContext>>();
        var userDbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<UserDbContext>>();
        var job = new DifficultyAdjustmentJob(
            jitenDbFactory,
            userDbFactory,
            NullLogger<DifficultyAdjustmentJob>.Instance);
        await job.ComputeAllAdjustments();

        await using var db = await jitenDbFactory.CreateDbContextAsync();
        var dd1 = await db.DeckDifficulties.FindAsync(1);
        var dd4 = await db.DeckDifficulties.FindAsync(4);

        dd1.Should().NotBeNull();
        dd4.Should().NotBeNull();

        // Deck 1 consistently voted easiest: adjustment should be negative (pushed down)
        dd1!.UserAdjustment.Should().BeLessThan(0);
        // Deck 4 consistently voted hardest: adjustment should be positive (pushed up)
        dd4!.UserAdjustment.Should().BeGreaterThan(0);

        dd1.EasierVoteCount.Should().BeGreaterThan(0);
        dd4.HarderVoteCount.Should().BeGreaterThan(0);
    }
}
