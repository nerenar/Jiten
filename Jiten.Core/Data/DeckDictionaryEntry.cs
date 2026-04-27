using System.Text.Json.Serialization;

namespace Jiten.Core.Data;

public class DeckDictionaryEntry
{
    public int DeckDictionaryEntryId { get; set; }
    public int DeckId { get; set; }
    public string Surface { get; set; } = string.Empty;
    public DeckDictionaryEntryType EntryType { get; set; }

    [JsonIgnore]
    public Deck Deck { get; set; } = null!;
}
