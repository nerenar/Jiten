using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Jimaku;

public class JimakuEntry
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public required string Name { get; set; }

    [JsonPropertyName("flags")] public required JimakuFlags Flags { get; set; }

    [JsonPropertyName("last_modified")] public DateTime LastModified { get; set; }

    [JsonPropertyName("anilist_id")] public int? AnilistId { get; set; }

    [JsonPropertyName("english_name")] public string? EnglishName { get; set; }

    [JsonPropertyName("japanese_name")] public required string JapaneseName { get; set; }

    [JsonPropertyName("creator_id")] public int CreatorId { get; set; }

    [JsonPropertyName("tmdb_id")] public string? TmdbId { get; set; }

    public List<JimakuFile>? Files { get; set; }
}