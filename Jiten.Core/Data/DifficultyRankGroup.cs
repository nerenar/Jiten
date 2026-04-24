namespace Jiten.Core.Data;

public class DifficultyRankGroup
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public MediaTypeGroup MediaTypeGroup { get; set; }
    public int SortIndex { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<DifficultyRankItem> Items { get; set; } = [];
}
