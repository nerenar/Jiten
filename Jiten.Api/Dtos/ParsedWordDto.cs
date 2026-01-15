using Jiten.Core.Data;

namespace Jiten.Api.Dtos;

public class ParsedWordDto
{
    public int WordId { get; set; }
    public string OriginalText { get; set; } = string.Empty;
    public byte ReadingIndex { get; set; }
    public List<string> Conjugations { get; set; } = new();

    public ParsedWordDto(string originalText)
    {
        OriginalText = originalText;
    }

    public ParsedWordDto(DeckWord deckWord)
    {
        WordId = deckWord.WordId;
        OriginalText = deckWord.OriginalText;
        ReadingIndex = deckWord.ReadingIndex;
        Conjugations = deckWord.Conjugations;
    }
}
