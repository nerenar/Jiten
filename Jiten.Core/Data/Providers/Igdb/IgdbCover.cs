using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Igdb;

public class IgdbCover
{
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}