namespace Jiten.Core.Data;

public class BlacklistedComparisonDeck
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int DeckId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Deck Deck { get; set; } = null!;
}
