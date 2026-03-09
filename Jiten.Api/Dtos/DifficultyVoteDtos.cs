using Jiten.Core.Data;

namespace Jiten.Api.Dtos;

public class DifficultyVoteDto
{
    public int Id { get; set; }
    public DeckSummaryDto DeckA { get; set; } = null!;
    public DeckSummaryDto DeckB { get; set; } = null!;
    public ComparisonOutcome Outcome { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class DifficultyRatingDto
{
    public int Id { get; set; }
    public int DeckId { get; set; }
    public string DeckTitle { get; set; } = null!;
    public string? RomajiTitle { get; set; }
    public string? EnglishTitle { get; set; }
    public string? CoverUrl { get; set; }
    public MediaType MediaType { get; set; }
    public int Rating { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class DeckSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? RomajiTitle { get; set; }
    public string? EnglishTitle { get; set; }
    public string? CoverUrl { get; set; }
    public float Difficulty { get; set; }
    public MediaType MediaType { get; set; }
}

public class SubmitVoteRequest
{
    public int DeckAId { get; set; }
    public int DeckBId { get; set; }
    public ComparisonOutcome Outcome { get; set; }
}

public class SubmitRatingRequest
{
    public int DeckId { get; set; }
    public int Rating { get; set; }
}

public class ComparisonSuggestionDto
{
    public DeckSummaryDto DeckA { get; set; } = null!;
    public DeckSummaryDto DeckB { get; set; } = null!;
}

public class SkipPairRequest
{
    public int DeckAId { get; set; }
    public int DeckBId { get; set; }
    public bool Permanent { get; set; }
}

public class VotingStatsDto
{
    public int TotalComparisons { get; set; }
    public int TotalRatings { get; set; }
    public int? Percentile { get; set; }
}

public class CompletedDecksResponse
{
    public DeckSummaryDto[] Decks { get; set; } = [];
    public int[][] VotedPairs { get; set; } = [];
}

public class BlacklistedDeckDto
{
    public int DeckId { get; set; }
    public string Title { get; set; } = null!;
    public string? RomajiTitle { get; set; }
    public string? EnglishTitle { get; set; }
    public string? CoverUrl { get; set; }
    public MediaType MediaType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
