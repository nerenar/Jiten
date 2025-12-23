namespace Jiten.Core.Data.Providers.Anilist;

public class AnilistRelationEdge
{
    public string RelationType { get; set; } = string.Empty;
    public required AnilistRelationNode Node { get; set; }
}
