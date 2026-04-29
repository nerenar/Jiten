using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Jikan;

public class JikanCharacterDetail
{
    [JsonPropertyName("mal_id")]
    public int MalId { get; set; }

    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_kanji")]
    public string? NameKanji { get; set; }
}
