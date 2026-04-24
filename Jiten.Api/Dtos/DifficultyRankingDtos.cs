using Jiten.Core.Data;

namespace Jiten.Api.Dtos;

public enum DifficultyRankingMoveMode
{
    Merge = 0,
    Insert = 1,
    Unrank = 2
}

public class DifficultyRankingMoveRequest
{
    public int DeckId { get; set; }
    public DifficultyRankingMoveMode Mode { get; set; }
    public int? TargetGroupId { get; set; }
    public int? InsertIndex { get; set; }
}

public class DifficultyRankGroupDto
{
    public int Id { get; set; }
    public int SortIndex { get; set; }
    public List<DeckSummaryDto> Decks { get; set; } = [];
}

public class DifficultyRankingSectionDto
{
    public MediaTypeGroup Group { get; set; }
    public List<DifficultyRankGroupDto> Groups { get; set; } = [];
    public List<DeckSummaryDto> Unranked { get; set; } = [];
}
