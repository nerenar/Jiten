namespace Jiten.Api.Dtos;

public class WordReplacementResult
{
    public int DeckWordsUpdated { get; set; }
    public int DeckWordsMerged { get; set; }
    public int ExampleSentenceWordsUpdated { get; set; }
    public int FsrsCardsUpdated { get; set; }
    public int FsrsCardsSkipped { get; set; }
    public int AffectedDeckCount { get; set; }
    public int ParentDecksQueued { get; set; }
    public bool WasDryRun { get; set; }
}
