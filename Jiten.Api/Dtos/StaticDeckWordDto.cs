namespace Jiten.Api.Dtos;

public class StaticDeckWordDto : DictionaryEntryDto
{
    public int Occurrences { get; set; }
    public int DeckSortOrder { get; set; }
}

public class StaticDeckWordsResponse
{
    public string DeckName { get; set; } = "";
    public List<StaticDeckWordDto> Words { get; set; } = [];
}
