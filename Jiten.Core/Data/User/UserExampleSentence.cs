namespace Jiten.Core.Data.User;

public class UserExampleSentence
{
    public int UserExampleSentenceId { get; set; }
    public string UserId { get; set; } = default!;
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
    public required string Text { get; set; }
    public string? Source { get; set; }
    public byte SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
