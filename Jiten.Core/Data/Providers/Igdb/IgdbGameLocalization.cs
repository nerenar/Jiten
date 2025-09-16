using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Igdb;

public class IgdbGameLocalization
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("region")]
    public int Region { get; set; }
}