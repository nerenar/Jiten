namespace Jiten.Api.Dtos;

public class SplitWordResult
{
    public int DeckWordsDeleted { get; set; }
    public int DeckWordsInserted { get; set; }
    public int DeckWordsMerged { get; set; }
    public int ExampleSentenceWordsDeleted { get; set; }
    public int ExampleSentenceWordsInserted { get; set; }
    public int AffectedDeckCount { get; set; }
    public int ParentDecksQueued { get; set; }
    public bool WasDryRun { get; set; }
}
