namespace Jiten.Api.Dtos;

public class IssuesDto
{
    public List<int> MissingRomajiTitles { get; set; } = new();
    public List<int> MissingLinks { get; set; } = new();
    public List<int> ZeroCharacters { get; set; } = new();
}