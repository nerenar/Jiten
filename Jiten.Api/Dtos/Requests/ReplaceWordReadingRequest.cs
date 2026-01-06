namespace Jiten.Api.Dtos.Requests;

public class ReplaceWordReadingRequest
{
    public int OldWordId { get; set; }
    public byte OldReadingIndex { get; set; }
    public int NewWordId { get; set; }
    public byte NewReadingIndex { get; set; }
    public bool DryRun { get; set; } = false;
}
