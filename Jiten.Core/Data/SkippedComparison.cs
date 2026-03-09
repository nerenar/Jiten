namespace Jiten.Core.Data;

public class SkippedComparison
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int DeckLowId { get; set; }
    public int DeckHighId { get; set; }
    public bool Permanent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
