namespace Jiten.Core.Data.Providers;

public class MetadataRelation
{
    public string ExternalId { get; set; } = string.Empty;
    public LinkType LinkType { get; set; }
    public DeckRelationshipType RelationshipType { get; set; }
    public MediaType? TargetMediaType { get; set; }
    public bool SwapDirection { get; set; }
}
