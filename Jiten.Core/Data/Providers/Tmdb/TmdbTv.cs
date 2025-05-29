using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Tmdb;

public class TmdbTv
{
    [JsonPropertyName("id")] public int Id { get; set; }

    /// <summary>
    /// Japanese title
    /// </summary>
    [JsonPropertyName("original_name")]
    public string OriginalName { get; set; }

    /// <summary>
    /// English or romaji title
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("first_air_date")] public DateTime FirstAirDate { get; set; }

    [JsonPropertyName("poster_path")] public string PosterPath { get; set; }
}