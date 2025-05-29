using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Jimaku;

public class JimakuFlags
{
    [JsonPropertyName("anime")]
    public bool Anime { get; set; }

    [JsonPropertyName("unverified")]
    public bool Unverified { get; set; }

    [JsonPropertyName("external")]
    public bool External { get; set; }

    [JsonPropertyName("movie")]
    public bool Movie { get; set; }

    [JsonPropertyName("adult")]
    public bool Adult { get; set; }
}