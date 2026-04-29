using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Jikan;

public class JikanImages
{
    public JikanImageFormat? Jpg { get; set; }
}

public class JikanImageFormat
{
    [JsonPropertyName("large_image_url")]
    public string? LargeImageUrl { get; set; }
}
