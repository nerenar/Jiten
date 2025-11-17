namespace Jiten.Api.Dtos.Requests;

public class SetVocabularyStateRequest
{
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
    public required string State { get; set; }
}
