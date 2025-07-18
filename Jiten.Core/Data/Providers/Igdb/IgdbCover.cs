using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Igdb;

public class IgdbCover
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
}