namespace Jiten.Core.Data.Providers.Jikan;

public class JikanRelationGroup
{
    public string Relation { get; set; } = string.Empty;
    public List<JikanRelationEntry> Entry { get; set; } = [];
}
