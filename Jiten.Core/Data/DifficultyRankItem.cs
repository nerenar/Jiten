namespace Jiten.Core.Data;

public class DifficultyRankItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int GroupId { get; set; }
    public int DeckId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public DifficultyRankGroup Group { get; set; } = null!;
    public Deck Deck { get; set; } = null!;
}
