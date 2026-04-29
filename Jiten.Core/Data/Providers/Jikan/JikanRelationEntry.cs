using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Jikan;

public class JikanRelationEntry
{
    [JsonPropertyName("mal_id")]
    public int MalId { get; set; }

    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
