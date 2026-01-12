namespace Jiten.Api.Dtos;

public class ParseNormalisedResultDto
{
    public string NormalisedText { get; set; } = string.Empty;
    public List<DeckWordDto> Words { get; set; } = new();
}
