using Jiten.Core.Data;

namespace Jiten.Api.Dtos;

public class DeckDictionaryEntryDto
{
    public string Surface { get; set; } = string.Empty;
    public DeckDictionaryEntryType EntryType { get; set; }
}
