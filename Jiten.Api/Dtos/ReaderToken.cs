namespace Jiten.Api.Dtos;

public class ReaderToken
{
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public int Length { get; set; }
    public List<string> Conjugations { get; set; } = new();
}