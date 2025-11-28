namespace Jiten.Core.Data.FSRS;

public class FsrsImportResultDto
{
    public required int CardsImported { get; set; }
    public required int CardsSkipped { get; set; }
    public required int CardsUpdated { get; set; }
    public required int ReviewLogsImported { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
}
