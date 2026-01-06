namespace Jiten.Api.Dtos.Requests;

public class RemoveWordRequest
{
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
    public bool DryRun { get; set; } = false;
}
