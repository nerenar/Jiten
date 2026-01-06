namespace Jiten.Api.Dtos.Requests;

public class SplitWordRequest
{
    public int OldWordId { get; set; }
    public byte OldReadingIndex { get; set; }
    public List<WordReadingPair> NewWords { get; set; } = [];
    public bool DryRun { get; set; } = false;
}

public class WordReadingPair
{
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
}
