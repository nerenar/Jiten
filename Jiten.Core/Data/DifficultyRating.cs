namespace Jiten.Core.Data;

public class DifficultyRating
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int DeckId { get; set; }
    public int Rating { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public Deck Deck { get; set; } = null!;
}
