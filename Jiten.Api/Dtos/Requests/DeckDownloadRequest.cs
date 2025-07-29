namespace Jiten.Api.Dtos.Requests;

public class DeckDownloadRequest
{
    public DeckFormat Format { get; set; }
    public DeckDownloadType DownloadType { get; set; }
    public DeckOrder Order { get; set; }
    public int MinFrequency { get; set; }
    public int MaxFrequency { get; set; }
    public bool ExcludeKana { get; set; }
    public bool ExcludeKnownWords { get; set; }
    public bool ExcludeExampleSentences { get; set; }
    public List<long>? KnownWordIds { get; set; }
}
