namespace Jiten.Core.Data;

public enum ComparisonOutcome
{
    MuchEasier = -2,
    Easier = -1,
    Same = 0,
    Harder = 1,
    MuchHarder = 2
}

public class DifficultyVote
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;

    public int DeckLowId { get; set; }
    public int DeckHighId { get; set; }

    public ComparisonOutcome Outcome { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsValid { get; set; } = true;

    public Deck DeckLow { get; set; } = null!;
    public Deck DeckHigh { get; set; } = null!;
}
