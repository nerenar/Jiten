namespace Jiten.Api.Dtos;

public class RemoveWordResult
{
    public int DeckWordsDeleted { get; set; }
    public int ExampleSentenceWordsDeleted { get; set; }
    public int AffectedDeckCount { get; set; }
    public int ParentDecksQueued { get; set; }
    public bool WasDryRun { get; set; }
}
