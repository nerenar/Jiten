using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Jikan;

public class JikanMedia
{
    [JsonPropertyName("mal_id")]
    public int MalId { get; set; }

    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("title_english")]
    public string? TitleEnglish { get; set; }

    [JsonPropertyName("title_japanese")]
    public string? TitleJapanese { get; set; }

    [JsonPropertyName("title_synonyms")]
    public List<string> TitleSynonyms { get; set; } = [];

    public List<JikanTitle> Titles { get; set; } = [];
    public string? Type { get; set; }
    public int? Episodes { get; set; }
    public string? Status { get; set; }
    public JikanAired? Aired { get; set; }
    public JikanAired? Published { get; set; }
    public double? Score { get; set; }
    public string? Synopsis { get; set; }
    public JikanImages? Images { get; set; }
    public List<JikanGenre> Genres { get; set; } = [];
    public List<JikanGenre> Themes { get; set; } = [];
    public List<JikanGenre> Demographics { get; set; } = [];
    public List<JikanRelationGroup> Relations { get; set; } = [];
    public string? Rating { get; set; }
}
