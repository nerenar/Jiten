namespace Jiten.Api.Dtos;

public class ParseNormalisedResultDto
{
    public string NormalisedText { get; set; } = string.Empty;
    public List<ParsedWordDto> Words { get; set; } = new();
}
