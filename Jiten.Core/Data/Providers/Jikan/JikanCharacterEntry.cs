using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Jikan;

public class JikanCharacterEntry
{
    public JikanCharacter Character { get; set; } = new();
    public string Role { get; set; } = string.Empty;
}

public class JikanCharacter
{
    [JsonPropertyName("mal_id")]
    public int MalId { get; set; }

    public string Name { get; set; } = string.Empty;
}
