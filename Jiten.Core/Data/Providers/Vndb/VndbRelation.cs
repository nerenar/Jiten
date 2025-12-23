using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Vndb;

public class VndbRelation
{
    public string Id { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;

    [JsonPropertyName("relation_official")]
    public bool RelationOfficial { get; set; }
}
